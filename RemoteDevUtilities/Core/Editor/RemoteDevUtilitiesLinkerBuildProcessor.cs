using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Build
{
    /// <summary>
    /// Supplies UnityLinker with a build-only preservation manifest. Keeping the
    /// generated file in Library avoids importing a temporary Asset or creating
    /// a meta file in the consuming project.
    /// </summary>
    internal sealed class RemoteDevUtilitiesLinkerBuildProcessor : IUnityLinkerProcessor, IPostprocessBuildWithReport
    {
        private const string GeneratedDirectoryName = "RemoteDevUtilities/UnityLinker";

        public int callbackOrder => -1000;

        [InitializeOnLoadMethod]
        private static void CleanupAfterInterruptedBuild()
        {
            if (!BuildPipeline.isBuildingPlayer)
                DeleteGeneratedManifest();
        }

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            DeleteGeneratedManifest();

#if ENABLE_DEBUG
            if (!SAS.Utilities.RemoteDevUtilities.Editor.Configuration.RemoteDevUtilitiesProjectSettings.instance.Runtime.EnableRemoteAgent)
            {
                return null;
            }

            string packageRoot = ResolvePackageRoot();
            IReadOnlyList<string> assemblies = RemoteDevUtilitiesLinkerManifest.DiscoverPreservedAssemblyNames(packageRoot);
            if (assemblies.Count == 0)
                throw new BuildFailedException("Remote Dev Utilities could not find any runtime assemblies to preserve for UnityLinker.");

            string outputPath = GetGeneratedManifestPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath,RemoteDevUtilitiesLinkerManifest.Create(assemblies),new UTF8Encoding(false));
            ScheduleCleanup();
            return outputPath;
#else
            return null;
#endif
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            DeleteGeneratedManifest();
        }

        private static string ResolvePackageRoot()
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RemoteDevUtilitiesLinkerBuildProcessor).Assembly);
            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath))
            {
                throw new BuildFailedException("Remote Dev Utilities could not resolve its installed package path while generating the UnityLinker manifest.");
            }

            return package.resolvedPath;
        }

        private static string GetGeneratedManifestPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new BuildFailedException("Remote Dev Utilities could not resolve the Unity project root while generating the UnityLinker manifest.");
            }
            return Path.GetFullPath(Path.Combine(projectRoot, "Library", GeneratedDirectoryName, "link.xml"));
        }

        private static void ScheduleCleanup()
        {
            EditorApplication.delayCall -= CleanupWhenBuildStops;
            EditorApplication.delayCall += CleanupWhenBuildStops;
        }

        private static void CleanupWhenBuildStops()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                ScheduleCleanup();
                return;
            }

            DeleteGeneratedManifest();
        }

        private static void DeleteGeneratedManifest()
        {
            string path = GetGeneratedManifestPath();
            if (File.Exists(path))
                File.Delete(path);

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                Directory.Delete(directory);
            }
        }
    }

    internal static class RemoteDevUtilitiesLinkerManifest
    {
        private const string CoreAssembly = "DevUtilities";
        private const string RemoteCoreAssembly = "DevUtilities.RemoteDevUtilities";
        private const string RemoteAssemblyPrefix = "DevUtilities.RemoteDevUtilities.";
        private const string InputVisualizerAssembly = "SAS.DevUtility.MiniTools.InputVisualizer";

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string name;
        }

        internal static IReadOnlyList<string> DiscoverPreservedAssemblyNames(string packageRoot)
        {
            if (string.IsNullOrWhiteSpace(packageRoot) || !Directory.Exists(packageRoot))
            {
                throw new DirectoryNotFoundException($"Remote Dev Utilities package path was not found: '{packageRoot}'.");
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories))
            {
                AssemblyDefinition definition = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));
                string assemblyName = definition?.name;
                if (ShouldPreserve(assemblyName))
                    names.Add(assemblyName);
            }

            return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        }

        internal static string Create(IEnumerable<string> assemblyNames)
        {
            if (assemblyNames == null)
                throw new ArgumentNullException(nameof(assemblyNames));

            var builder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                OmitXmlDeclaration = true
            };

            using (XmlWriter writer = XmlWriter.Create(builder, settings))
            {
                writer.WriteStartElement("linker");
                foreach (string assemblyName in assemblyNames.Where(name => !string.IsNullOrWhiteSpace(name))
                             .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
                {
                    writer.WriteStartElement("assembly");
                    writer.WriteAttributeString("fullname", assemblyName);
                    writer.WriteAttributeString("preserve", "all");
                    writer.WriteAttributeString("ignoreIfMissing", "1");
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            builder.Append('\n');
            return builder.ToString();
        }

        private static bool ShouldPreserve(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return false;

            if (string.Equals(assemblyName, CoreAssembly, StringComparison.Ordinal) ||
                string.Equals(assemblyName, RemoteCoreAssembly, StringComparison.Ordinal) ||
                string.Equals(assemblyName, InputVisualizerAssembly, StringComparison.Ordinal))
            {
                return true;
            }

            return assemblyName.StartsWith(RemoteAssemblyPrefix, StringComparison.Ordinal) &&
                   (assemblyName.EndsWith(".Runtime", StringComparison.Ordinal) ||
                    assemblyName.EndsWith(".Protocol", StringComparison.Ordinal));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SAS.DevUtilities;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.Presentation;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding
{
    internal enum MiniToolScaffoldTemplate
    {
        Snapshot,
        Collector,
        SnapshotProvider,
        DataProvider,
        View,
        LocalController,
        Command
    }

    internal static class MiniToolScaffoldTemplateRenderer
    {
        private const string TemplateFolderName = "Templates";

        internal static string Render(MiniToolScaffoldTemplate template, MiniToolScaffoldState state)
        {
            if (state?.Request == null)
                throw new ArgumentNullException(nameof(state));

            string source = LoadTemplate(template);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["{{NAMESPACE}}"] = state.Request.Namespace.Trim(),
                ["{{TOOL_NAME}}"] = state.ClassName,
                ["{{UPDATE_INTERVAL}}"] = Math.Max(0.1f, state.Request.UpdateInterval).ToString("0.###", CultureInfo.InvariantCulture),
                ["{{CONTRACT_NAMESPACE}}"] = typeof(IMiniToolSnapshot).Namespace,
                ["{{REMOTE_PROVIDER_NAMESPACE}}"] = typeof(MiniToolDataProvider<>).Namespace,
                ["{{CONSOLE_NAMESPACE}}"] = typeof(ConsoleCommand).Namespace,
                ["{{PRESENTATION_NAMESPACE}}"] = typeof(DevUtilityPresentation).Namespace
            };

            foreach (KeyValuePair<string, string> replacement in replacements)
                source = source.Replace(replacement.Key, replacement.Value);

            if (source.IndexOf("{{", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException($"Mini-tool template '{template}' contains an unresolved token.");

            return source;
        }

        private static string LoadTemplate(MiniToolScaffoldTemplate template)
        {
            string rendererPath = FindRendererPath();
            string directory = Path.GetDirectoryName(rendererPath)?.Replace('\\', '/');
            string templatePath = $"{directory}/{TemplateFolderName}/{GetTemplateFileName(template)}";
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(templatePath);
            if (asset == null)
                throw new FileNotFoundException($"Mini-tool scaffold template was not found at '{templatePath}'.", templatePath);

            return asset.text;
        }

        private static string FindRendererPath()
        {
            foreach (string guid in AssetDatabase.FindAssets(nameof(MiniToolScaffoldTemplateRenderer) + " t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == typeof(MiniToolScaffoldTemplateRenderer))
                    return path;
            }

            throw new FileNotFoundException("Could not locate the mini-tool scaffold template renderer script.");
        }

        private static string GetTemplateFileName(MiniToolScaffoldTemplate template)
        {
            return template switch
            {
                MiniToolScaffoldTemplate.Snapshot => "MiniToolSnapshot.cs.txt",
                MiniToolScaffoldTemplate.Collector => "MiniToolCollector.cs.txt",
                MiniToolScaffoldTemplate.SnapshotProvider => "MiniToolSnapshotProvider.cs.txt",
                MiniToolScaffoldTemplate.DataProvider => "MiniToolDataProvider.cs.txt",
                MiniToolScaffoldTemplate.View => "MiniToolView.cs.txt",
                MiniToolScaffoldTemplate.LocalController => "MiniToolLocalController.cs.txt",
                MiniToolScaffoldTemplate.Command => "MiniToolCommand.cs.txt",
                _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
            };
        }
    }
}

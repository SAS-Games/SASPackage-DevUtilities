using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding
{
    internal static class MiniToolScaffoldGenerator
    {
        internal static bool TryBegin(MiniToolScaffoldRequest request, out string error)
        {
            if (request == null)
            {
                error = "Mini-tool scaffold request is missing.";
                return false;
            }

            if (!request.TryValidate(out error))
                return false;
            if (MiniToolScaffoldPersistence.HasPending)
            {
                error = "Another mini-tool scaffold is waiting for Unity to finish compiling. Complete or cancel it before creating another one.";
                return false;
            }

            MiniToolScaffoldState state = CreateState(request);
            if (!TryValidateDestination(state, out error))
                return false;

            try
            {
                EnsureAssetFolder(state.TargetFolder);
                WriteScript(state.SnapshotScriptPath, MiniToolScaffoldTemplate.Snapshot, state);
                WriteScript(state.CollectorScriptPath, MiniToolScaffoldTemplate.Collector, state);
                WriteScript(state.SnapshotProviderScriptPath, MiniToolScaffoldTemplate.SnapshotProvider, state);
                WriteScript(state.DataProviderScriptPath, MiniToolScaffoldTemplate.DataProvider, state);
                WriteScript(state.ViewScriptPath, MiniToolScaffoldTemplate.View, state);
                WriteScript(state.LocalControllerScriptPath, MiniToolScaffoldTemplate.LocalController, state);
                if (request.CreateCommand)
                    WriteScript(state.CommandScriptPath, MiniToolScaffoldTemplate.Command, state);

                MiniToolScaffoldPersistence.Save(state);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                CompilationPipeline.RequestScriptCompilation();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                MiniToolScaffoldPersistence.Clear();
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        internal static MiniToolScaffoldState CreateState(MiniToolScaffoldRequest request)
        {
            string className = MiniToolScaffoldNaming.ToIdentifier(request.ToolName);
            string outputFolder = MiniToolScaffoldNaming.NormalizeAssetPath(request.OutputFolder);
            string targetFolder = request.CreateSubfolder ? $"{outputFolder}/{className}" : outputFolder;
            return new MiniToolScaffoldState
            {
                Request = request,
                ClassName = className,
                TargetFolder = targetFolder,
                SnapshotScriptPath = $"{targetFolder}/{className}Snapshot.cs",
                CollectorScriptPath = $"{targetFolder}/{className}Collector.cs",
                SnapshotProviderScriptPath = $"{targetFolder}/{className}SnapshotProvider.cs",
                DataProviderScriptPath = $"{targetFolder}/{className}DataProvider.cs",
                ViewScriptPath = $"{targetFolder}/{className}View.cs",
                LocalControllerScriptPath = $"{targetFolder}/{className}LocalController.cs",
                CommandScriptPath = request.CreateCommand ? $"{targetFolder}/{className}Command.cs" : string.Empty,
                PrefabPath = $"{targetFolder}/{className}.prefab",
                CommandAssetPath = request.CreateCommand ? $"{targetFolder}/{className}Command.asset" : string.Empty,
                DefinitionPath = $"{targetFolder}/{className} Mini Tool.asset"
            };
        }

        private static bool TryValidateDestination(MiniToolScaffoldState state, out string error)
        {
            var paths = new List<string>
            {
                state.SnapshotScriptPath,
                state.CollectorScriptPath,
                state.SnapshotProviderScriptPath,
                state.DataProviderScriptPath,
                state.ViewScriptPath,
                state.LocalControllerScriptPath,
                state.PrefabPath,
                state.DefinitionPath
            };
            if (state.Request.CreateCommand)
            {
                paths.Add(state.CommandScriptPath);
                paths.Add(state.CommandAssetPath);
            }

            foreach (string path in paths)
            {
                if (!File.Exists(MiniToolScaffoldNaming.ToAbsolutePath(path)) && !AssetDatabase.LoadMainAssetAtPath(path))
                    continue;

                error = $"'{path}' already exists. Choose another tool name or output folder.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void WriteScript(string path, MiniToolScaffoldTemplate template, MiniToolScaffoldState state)
        {
            string source = MiniToolScaffoldTemplateRenderer.Render(template, state);
            File.WriteAllText(MiniToolScaffoldNaming.ToAbsolutePath(path), source, new UTF8Encoding(false));
        }

        private static void EnsureAssetFolder(string folder)
        {
            string normalized = MiniToolScaffoldNaming.NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] segments = normalized.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }

    internal static class MiniToolScaffoldPersistence
    {
        private const string PendingDirectory = "Library/RemoteDevUtilities";
        private const string PendingFileName = "PendingMiniToolScaffold.json";

        internal static bool HasPending => File.Exists(GetPendingPath());

        internal static void Save(MiniToolScaffoldState state)
        {
            string path = GetPendingPath();
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(state, true), new UTF8Encoding(false));
        }

        internal static bool TryLoad(out MiniToolScaffoldState state)
        {
            state = null;
            string path = GetPendingPath();
            if (!File.Exists(path))
                return false;

            try
            {
                state = JsonUtility.FromJson<MiniToolScaffoldState>(File.ReadAllText(path));
                return state?.Request != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Remote Dev Utilities] Could not read the pending mini-tool scaffold: " + exception.GetBaseException().Message);
                return false;
            }
        }

        internal static void Clear()
        {
            string path = GetPendingPath();
            if (File.Exists(path))
                File.Delete(path);
        }

        private static string GetPendingPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, PendingDirectory, PendingFileName));
        }
    }
}

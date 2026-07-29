#if ENABLE_DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    /// <summary>
    /// Temporarily adds validated definitions to the build's preloaded assets.
    /// PlayerSettings are restored immediately afterwards.
    /// </summary>
    internal sealed class MiniToolBuildProcessor :
        IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        private static readonly string BackupPath = Path.Combine(
            "Library",
            "RemoteDevUtilities",
            "MiniToolPreloadedAssetsBackup.txt");

        public int callbackOrder => 100;

        [InitializeOnLoadMethod]
        private static void RestoreAfterInterruptedBuild()
        {
            ScheduleRestore();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RestorePreloadedAssets();

            UnityEngine.Object[] previous =
                PlayerSettings.GetPreloadedAssets();
            WriteBackup(previous);

            var combined = new List<UnityEngine.Object>(
                previous ?? Array.Empty<UnityEngine.Object>());
            foreach (MiniToolDefinition definition in
                     MiniToolRegistry.GetDefinitions())
            {
                if (definition != null && !combined.Contains(definition))
                    combined.Add(definition);
            }

            try
            {
                PlayerSettings.SetPreloadedAssets(combined.ToArray());
                ScheduleRestore();
            }
            catch
            {
                RestorePreloadedAssets();
                throw;
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestorePreloadedAssets();
        }

        private static void RestorePreloadedAssets()
        {
            if (!File.Exists(BackupPath))
                return;

            string serialized = File.ReadAllText(BackupPath);
            PlayerSettings.SetPreloadedAssets(
                DeserializeAssets(serialized));
            File.Delete(BackupPath);
        }

        private static void ScheduleRestore()
        {
            EditorApplication.delayCall -= RestoreWhenBuildStops;
            EditorApplication.delayCall += RestoreWhenBuildStops;
        }

        private static void RestoreWhenBuildStops()
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                ScheduleRestore();
                return;
            }

            RestorePreloadedAssets();
        }

        private static void WriteBackup(
            IEnumerable<UnityEngine.Object> assets)
        {
            string directory = Path.GetDirectoryName(BackupPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                BackupPath,
                SerializeAssetGuids(assets));
        }

        private static string SerializeAssetGuids(
            IEnumerable<UnityEngine.Object> assets)
        {
            var guids = new List<string>();
            foreach (UnityEngine.Object asset in
                     assets ?? Array.Empty<UnityEngine.Object>())
            {
                string path = AssetDatabase.GetAssetPath(asset);
                string guid = string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrWhiteSpace(guid))
                    guids.Add(guid);
            }

            return string.Join("|", guids);
        }

        private static UnityEngine.Object[] DeserializeAssets(
            string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return Array.Empty<UnityEngine.Object>();

            var assets = new List<UnityEngine.Object>();
            foreach (string guid in serialized.Split('|'))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset =
                    string.IsNullOrWhiteSpace(path)
                        ? null
                        : AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets.ToArray();
        }
    }
}
#endif

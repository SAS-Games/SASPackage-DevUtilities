#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    /// <summary>
    /// Owns the original Player preloaded-assets backup shared by Dev Utilities
    /// build processors. Contributors may safely add assets without maintaining
    /// nested backups that can restore in the wrong order.
    /// </summary>
    internal static class DevUtilitiesPreloadedBuildAssets
    {
        private static readonly string BackupPath = Path.Combine(
            "Library",
            "DevUtilities",
            "PreloadedAssetsBackup.txt");
        private static readonly string LegacyRemoteBackupPath = Path.Combine(
            "Library",
            "RemoteDevUtilities",
            "PreloadedAssetsBackup.txt");

        internal static void Begin()
        {
            if (File.Exists(BackupPath))
            {
                DeleteLegacyBackup();
                return;
            }

            // Recover the backup used before preloaded-asset ownership was moved
            // into the shared Dev Utilities build scope.
            RestoreFrom(LegacyRemoteBackupPath);

            UnityEngine.Object[] assets = PlayerSettings.GetPreloadedAssets();
            string directory = Path.GetDirectoryName(BackupPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(BackupPath, SerializeAssetGuids(assets));
        }

        internal static void Add(IEnumerable<UnityEngine.Object> assets)
        {
            Begin();

            var combined = new List<UnityEngine.Object>(
                PlayerSettings.GetPreloadedAssets() ?? Array.Empty<UnityEngine.Object>());
            foreach (UnityEngine.Object asset in assets ?? Array.Empty<UnityEngine.Object>())
            {
                if (asset != null && !combined.Contains(asset))
                    combined.Add(asset);
            }

            PlayerSettings.SetPreloadedAssets(combined.ToArray());
        }

        internal static void Restore()
        {
            if (RestoreFrom(BackupPath))
            {
                DeleteLegacyBackup();
                return;
            }

            RestoreFrom(LegacyRemoteBackupPath);
        }

        private static void DeleteLegacyBackup()
        {
            if (File.Exists(LegacyRemoteBackupPath))
                File.Delete(LegacyRemoteBackupPath);
        }

        private static bool RestoreFrom(string backupPath)
        {
            if (!File.Exists(backupPath))
                return false;

            string serialized = File.ReadAllText(backupPath);
            PlayerSettings.SetPreloadedAssets(DeserializeAssets(serialized));
            File.Delete(backupPath);
            return true;
        }

        private static string SerializeAssetGuids(IEnumerable<UnityEngine.Object> assets)
        {
            var guids = new List<string>();
            foreach (UnityEngine.Object asset in assets ?? Array.Empty<UnityEngine.Object>())
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

        private static UnityEngine.Object[] DeserializeAssets(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
                return Array.Empty<UnityEngine.Object>();

            var assets = new List<UnityEngine.Object>();
            foreach (string guid in serialized.Split('|'))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object asset = string.IsNullOrWhiteSpace(path)
                    ? null
                    : AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets.ToArray();
        }
    }

    public sealed class DebugBuildProcessor :
        IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        private const string GeneratedRuntimeSettingsPath =
            "Assets/DebugRuntimeConfig.generated.asset";

        public int callbackOrder => -1000;

        [InitializeOnLoadMethod]
        private static void RestoreAfterInterruptedBuild()
        {
            ScheduleRestore();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            RestoreBuildState();

#if !ENABLE_DEBUG
            return;
#endif

            DevUtilitiesPreloadedBuildAssets.Begin();
            try
            {
                DebugRuntimeConfig runtimeConfig = CreateRuntimeSettingsSnapshot();
                DevUtilitiesPreloadedBuildAssets.Add(new UnityEngine.Object[] { runtimeConfig });
                ScheduleRestore();
            }
            catch
            {
                RestoreBuildState();
                throw;
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RestoreBuildState();
        }

        private static DebugRuntimeConfig CreateRuntimeSettingsSnapshot()
        {
            DeleteGeneratedRuntimeSettings(true);

            DebugEditorSettings editorSettings = DebugEditorSettings.instance;

            DebugRuntimeConfig runtimeConfig = ScriptableObject.CreateInstance<DebugRuntimeConfig>();
            runtimeConfig.Apply(
                editorSettings.pauseOnEnable,
                editorSettings.logLevel,
                editorSettings.allowedTags,
                true);
            runtimeConfig.name = "DebugRuntimeConfig";
            runtimeConfig.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
            AssetDatabase.CreateAsset(runtimeConfig, GeneratedRuntimeSettingsPath);
            AssetDatabase.SaveAssets();
            return runtimeConfig;
        }

        private static void RestoreBuildState()
        {
            DevUtilitiesPreloadedBuildAssets.Restore();
            DeleteGeneratedRuntimeSettings();
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

            RestoreBuildState();
        }

        private static void DeleteGeneratedRuntimeSettings(bool failIfOccupied = false)
        {
            UnityEngine.Object existing =
                AssetDatabase.LoadMainAssetAtPath(GeneratedRuntimeSettingsPath);
            if (existing == null)
                return;

            if (existing is DebugRuntimeConfig settings && settings.IsBuildSnapshot)
            {
                AssetDatabase.DeleteAsset(GeneratedRuntimeSettingsPath);
                return;
            }

            if (failIfOccupied)
            {
                throw new BuildFailedException(
                    $"Cannot create the temporary Dev Utilities runtime settings snapshot because " +
                    $"'{GeneratedRuntimeSettingsPath}' is already occupied.");
            }
        }
    }
}
#endif

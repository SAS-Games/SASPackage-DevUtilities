using System.Collections.Generic;
using SAS.Utilities.DeveloperConsole.Editor;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    /// <summary>
    /// Temporarily adds validated definitions and the Remote Dev Utilities settings
    /// snapshot to the build's preloaded assets.
    /// </summary>
    internal sealed class RemoteDevUtilitiesBuildProcessor :
        IPreprocessBuildWithReport,
        IPostprocessBuildWithReport
    {
        private const string GeneratedRuntimeSettingsPath =
            "Assets/RemoteDevUtilitiesSettings.generated.asset";

        public int callbackOrder => 100;

        [InitializeOnLoadMethod]
        private static void CleanupAfterInterruptedBuild()
        {
            ScheduleCleanup();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            DeleteGeneratedRuntimeSettings();

#if ENABLE_DEBUG
            var additions = new List<UnityEngine.Object>();
            foreach (MiniToolDefinition definition in
                     MiniToolRegistry.GetDefinitions())
            {
                if (definition != null && !additions.Contains(definition))
                    additions.Add(definition);
            }

            try
            {
                RemoteDevUtilitiesRuntimeSettings runtimeSettings =
                    CreateRuntimeSettingsSnapshot();
                additions.Add(runtimeSettings);
                DevUtilitiesPreloadedBuildAssets.Add(additions);
                ScheduleCleanup();
            }
            catch
            {
                DevUtilitiesPreloadedBuildAssets.Restore();
                DeleteGeneratedRuntimeSettings();
                throw;
            }
#endif
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            DeleteGeneratedRuntimeSettings();
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

            DeleteGeneratedRuntimeSettings();
        }

        private static RemoteDevUtilitiesRuntimeSettings
            CreateRuntimeSettingsSnapshot()
        {
            DeleteGeneratedRuntimeSettings(true);

            RemoteDevUtilitiesRuntimeSettings settings =
                ScriptableObject.CreateInstance<
                    RemoteDevUtilitiesRuntimeSettings>();
            settings.Apply(
                RemoteDevUtilitiesProjectSettings.instance.Runtime,
                true);
            settings.name = "RemoteDevUtilitiesSettings";
            settings.hideFlags =
                HideFlags.HideInHierarchy | HideFlags.NotEditable;
            AssetDatabase.CreateAsset(
                settings,
                GeneratedRuntimeSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void DeleteGeneratedRuntimeSettings(
            bool failIfOccupied = false)
        {
            UnityEngine.Object existing =
                AssetDatabase.LoadMainAssetAtPath(
                    GeneratedRuntimeSettingsPath);
            if (existing == null)
                return;

            if (existing is RemoteDevUtilitiesRuntimeSettings settings &&
                settings.IsBuildSnapshot)
            {
                AssetDatabase.DeleteAsset(
                    GeneratedRuntimeSettingsPath);
                return;
            }

            if (failIfOccupied)
            {
                throw new BuildFailedException(
                    $"Cannot create the temporary Remote Dev Utilities " +
                    $"runtime settings snapshot because " +
                    $"'{GeneratedRuntimeSettingsPath}' is already occupied.");
            }
        }
    }
}

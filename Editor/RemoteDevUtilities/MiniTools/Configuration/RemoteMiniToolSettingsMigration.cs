using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    /// <summary>
    /// Repairs the serialized type identity left by the transition from the
    /// former visibility-only ScriptableSingleton to the unified settings
    /// object. Existing project configuration data remains unchanged.
    /// </summary>
    [InitializeOnLoad]
    internal static class RemoteMiniToolSettingsMigration
    {
        private const string SettingsPath =
            "ProjectSettings/RemoteDevUtilitiesMiniTools.asset";

        private const string LegacyTypeIdentity =
            "DevUtilities.RemoteDevUtilities.Editor::" +
            "SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration." +
            "RemoteMiniToolVisibilitySettings";

        private const string CurrentTypeIdentity =
            "DevUtilities.RemoteDevUtilities.Editor::" +
            "SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration." +
            "RemoteMiniToolSettings";

        static RemoteMiniToolSettingsMigration()
        {
            TryMigrate();
        }

        private static void TryMigrate()
        {
            try
            {
                string projectRoot =
                    Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                    return;

                string path = Path.Combine(
                    projectRoot,
                    SettingsPath);
                if (!File.Exists(path))
                    return;

                string serialized = File.ReadAllText(path);
                string legacy =
                    "m_EditorClassIdentifier: " + LegacyTypeIdentity;
                if (serialized.IndexOf(
                        legacy,
                        StringComparison.Ordinal) < 0)
                {
                    return;
                }

                string current =
                    "m_EditorClassIdentifier: " + CurrentTypeIdentity;
                File.WriteAllText(
                    path,
                    serialized.Replace(legacy, current));
                Debug.Log(
                    "[Remote Dev Utilities] Migrated the unified mini-tool " +
                    "project settings identity.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Remote Dev Utilities] Could not migrate the mini-tool " +
                    $"project settings: {exception.Message}");
            }
        }
    }
}

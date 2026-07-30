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
        private const string LegacySettingsPath =
            "ProjectSettings/RemoteDevUtilitiesMiniTools.asset";
        private const string SettingsPath =
            "ProjectSettings/RemoteDevUtilitiesSettings.asset";

        private const string LegacyTypeIdentity =
            "DevUtilities.RemoteDevUtilities.Editor::" +
            "SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration." +
            "RemoteMiniToolVisibilitySettings";

        private const string UnifiedMiniToolTypeIdentity =
            "DevUtilities.RemoteDevUtilities.Editor::" +
            "SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration." +
            "RemoteMiniToolSettings";
        private const string CurrentTypeIdentity =
            "DevUtilities.RemoteDevUtilities.Editor::" +
            "SAS.Utilities.RemoteDevUtilities.Editor.Configuration." +
            "RemoteDevUtilitiesProjectSettings";

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

                string legacyPath = Path.Combine(
                    projectRoot,
                    LegacySettingsPath);
                string path = Path.Combine(projectRoot, SettingsPath);
                bool changed = false;
                if (File.Exists(legacyPath) && !File.Exists(path))
                {
                    File.Move(legacyPath, path);
                    changed = true;
                }

                if (!File.Exists(path))
                    return;

                string serialized = File.ReadAllText(path);
                string current =
                    "m_EditorClassIdentifier: " + CurrentTypeIdentity;
                foreach (string previousIdentity in new[]
                         {
                             LegacyTypeIdentity,
                             UnifiedMiniToolTypeIdentity
                         })
                {
                    string previous =
                        "m_EditorClassIdentifier: " + previousIdentity;
                    if (serialized.IndexOf(
                            previous,
                            StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    serialized = serialized.Replace(previous, current);
                    changed = true;
                }

                if (!changed)
                    return;

                File.WriteAllText(path, serialized);
                Debug.Log(
                    "[Remote Dev Utilities] Migrated project settings to " +
                    SettingsPath + ".");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Remote Dev Utilities] Could not migrate the " +
                    $"project settings: {exception.Message}");
            }
        }
    }
}

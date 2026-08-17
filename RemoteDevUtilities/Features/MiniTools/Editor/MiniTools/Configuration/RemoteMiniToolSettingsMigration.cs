using System;
using System.IO;
using HP.Utilities.RemoteDevUtilities.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    /// <summary>
    /// Repairs the serialized type identity left by the transition from the
    /// former visibility-only ScriptableSingleton to the unified settings
    /// object. Existing project configuration data remains unchanged.
    /// </summary>
    [InitializeOnLoad]
    internal static class RemoteMiniToolSettingsMigration
    {
        private const string LegacySettingsPath = "ProjectSettings/RemoteDevUtilitiesMiniTools.asset";
        private const string SettingsPath = "ProjectSettings/RemoteDevUtilitiesSettings.asset";

        private const string TypeIdentityPrefix = "m_EditorClassIdentifier: ";
        private const string LegacyVisibilityTypeName = "RemoteMiniToolVisibilitySettings";
        private const string LegacyUnifiedTypeName = "RemoteMiniToolSettings";

        static RemoteMiniToolSettingsMigration()
        {
            TryMigrate();
        }

        private static void TryMigrate()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrWhiteSpace(projectRoot))
                    return;

                string legacyPath = Path.Combine(projectRoot, LegacySettingsPath);
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
                string currentIdentity = GetTypeIdentity(typeof(RemoteDevUtilitiesProjectSettings));
                serialized = ReplaceLegacyTypeIdentity(serialized, LegacyVisibilityTypeName, currentIdentity, ref changed);
                serialized = ReplaceLegacyTypeIdentity(serialized, LegacyUnifiedTypeName, currentIdentity, ref changed);

                if (!changed)
                    return;

                File.WriteAllText(path, serialized);
                Debug.Log("[Remote Dev Utilities] Migrated project settings to " + SettingsPath + ".");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Remote Dev Utilities] Could not migrate the " + $"project settings: {exception.Message}");
            }
        }

        private static string ReplaceLegacyTypeIdentity(string serialized, string legacyTypeName, string currentIdentity, ref bool changed)
        {
            int searchIndex = 0;
            while (searchIndex < serialized.Length)
            {
                int prefixIndex = serialized.IndexOf(TypeIdentityPrefix, searchIndex, StringComparison.Ordinal);
                if (prefixIndex < 0)
                    break;

                int identityStart = prefixIndex + TypeIdentityPrefix.Length;
                int lineEnd = serialized.IndexOf('\n', identityStart);
                if (lineEnd < 0)
                    lineEnd = serialized.Length;

                int identityEnd = lineEnd;
                if (identityEnd > identityStart && serialized[identityEnd - 1] == '\r')
                    identityEnd--;

                string identity = serialized.Substring(identityStart, identityEnd - identityStart);
                if (!identity.EndsWith("." + legacyTypeName, StringComparison.Ordinal))
                {
                    searchIndex = lineEnd + 1;
                    continue;
                }

                serialized = serialized.Substring(0, identityStart) + currentIdentity + serialized.Substring(identityEnd);
                changed = true;
                searchIndex = identityStart + currentIdentity.Length;
            }

            return serialized;
        }

        private static string GetTypeIdentity(Type type)
        {
            return $"{type.Assembly.GetName().Name}::{type.FullName}";
        }
    }
}

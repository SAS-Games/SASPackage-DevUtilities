using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    /// <summary>
    /// Focused facade for the presentation section of the unified mini-tool
    /// project settings.
    /// </summary>
    internal sealed class RemoteMiniToolPresentationSettings : ScriptableSingleton<RemoteMiniToolPresentationSettings>
    {
        internal static event Action Changed;

        internal RemoteMiniToolPresentationConfiguration Configuration => RemoteDevUtilitiesProjectSettings.instance.Presentations;

        internal bool TryGetOverride(string toolId, out GameObject prefab)
        {
            if (!Configuration.TryGetPrefabGuid(toolId, out string guid))
            {
                prefab = null;
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            prefab = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return true;
        }

        internal bool SetOverride(string toolId, GameObject prefab, out string error)
        {
            error = string.Empty;
            if (prefab == null)
            {
                ClearOverride(toolId);
                return true;
            }

            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                error = "Host presentation must be a prefab asset.";
                return false;
            }

            if (MiniToolRegistry.TryGet(toolId, out MiniToolRegistration registration) && !MiniToolRegistrationValidator.TryValidatePrefab(registration.Definition, prefab, out error))
            {
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
            {
                error = "The selected prefab does not have a valid asset GUID.";
                return false;
            }

            if (Configuration.SetPrefabGuid(toolId, guid))
                Persist();
            return true;
        }

        internal void ClearOverride(string toolId)
        {
            if (Configuration.Clear(toolId))
                Persist();
        }

        private void Persist()
        {
            RemoteDevUtilitiesProjectSettings.instance.Persist();
            Changed?.Invoke();
        }
    }
}

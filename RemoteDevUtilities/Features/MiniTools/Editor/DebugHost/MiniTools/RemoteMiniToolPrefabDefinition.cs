using System;
using System.Collections.Generic;
using System.Linq;
using HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools
{
    internal readonly struct RemoteMiniToolPrefabDefinition
    {
        public RemoteMiniToolPrefabDefinition(string toolId, string assetPath)
        {
            ToolId = toolId;
            AssetPath = assetPath;
        }

        public string ToolId { get; }
        public string AssetPath { get; }
    }

    internal static class RemoteMiniToolPrefabDefinitions
    {
        public static RemoteMiniToolPrefabDefinition[] Discover()
        {
            var definitions = new Dictionary<string, RemoteMiniToolPrefabDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (MiniToolRegistration registration in MiniToolRegistry.Registrations)
            {
                GameObject prefab = registration.LoadDebugHostPrefab();
                TryAdd(definitions, registration.Descriptor.Id, prefab == null ? string.Empty : AssetDatabase.GetAssetPath(prefab), replaceExisting: false);
            }

            foreach (RemoteMiniToolPresentationOverride presentationOverride in RemoteMiniToolPresentationSettings.instance.Configuration.Overrides)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(presentationOverride.PrefabGuid);
                TryAdd(definitions, presentationOverride.ToolId, assetPath, replaceExisting: true);
            }

            return definitions.Values.OrderBy(definition => definition.ToolId, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void TryAdd(IDictionary<string, RemoteMiniToolPrefabDefinition> definitions, string toolId, string assetPath, bool replaceExisting)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return;

            if (!string.IsNullOrWhiteSpace(assetPath) && AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
                return;

            if (!replaceExisting && definitions.ContainsKey(toolId))
                return;

            definitions[toolId] = new RemoteMiniToolPrefabDefinition(toolId, assetPath);
        }
    }
}

using System;
using System.Collections.Generic;
using HP.DevUtilities;
using HP.Utilities.Presentation;
using HP.Utilities.RemoteDevUtilities.MiniTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    /// <summary>
    /// Validates the relationship between a runtime provider and its optional
    /// Editor Debug Host presentation.
    /// </summary>
    internal static class MiniToolRegistrationValidator
    {
        private static readonly Type SnapshotViewType = typeof(IMiniToolSnapshotView<>);
        private static readonly Type StreamViewType = typeof(IMiniToolStreamView<>);

        internal static void Validate(MiniToolDefinition definition, string assetPath, ICollection<string> errors, ICollection<string> warnings)
        {
            if (definition == null || errors == null || warnings == null || !definition.TryGetProviderType(out Type providerType))
                return;

            string prefix = string.IsNullOrWhiteSpace(assetPath) ? string.Empty : assetPath + ": ";
            string prefabGuid = definition.DebugHostPrefabGuid;
            bool providesFields = MiniToolProviderCapabilities.ProvidesFields(providerType);
            bool providesTypedSnapshot = MiniToolProviderCapabilities.ProvidesTypedSnapshot(providerType);
            bool providesEventStream = MiniToolProviderCapabilities.ProvidesEventStream(providerType);

            if (string.IsNullOrWhiteSpace(prefabGuid))
            {
                if ((providesTypedSnapshot || providesEventStream) && !providesFields)
                    errors.Add(prefix + "The provider exposes Debug Host snapshot or events but no Debug Host prefab is assigned. " + "Assign a prefab with compatible snapshot and stream views.");
                else if (providesTypedSnapshot || providesEventStream)
                    warnings.Add(prefix + "The provider exposes typed Debug Host data but no Debug Host prefab is assigned. " + "The Debug Host will use the generic Native Workspace fields instead.");

                return;
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = string.IsNullOrWhiteSpace(prefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                errors.Add(prefix + $"Debug Host prefab GUID '{prefabGuid}' cannot be resolved to a prefab asset.");
                return;
            }

            ValidatePrefab(providerType, prefab, prefix, errors, warnings);
        }

        internal static bool TryValidatePrefab(MiniToolDefinition definition, GameObject prefab, out string error)
        {
            error = string.Empty;
            if (definition == null || prefab == null)
                return true;
            if (!definition.TryGetProviderType(out Type providerType))
            {
                error = "The mini-tool data provider cannot be loaded.";
                return false;
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            ValidatePrefab(providerType, prefab, string.Empty, errors, warnings);
            if (errors.Count == 0)
                return true;

            error = string.Join("\n", errors);
            return false;
        }

        private static void ValidatePrefab(Type providerType, GameObject prefab, string prefix, ICollection<string> errors, ICollection<string> warnings)
        {
            if (prefab.GetComponent<DevUtilityPresentation>() == null)
            {
                errors.Add(prefix + $"Debug Host prefab '{prefab.name}' must have DevUtilityPresentation on its root so requested visibility survives remote UI suppression.");
            }

            bool providesFields = MiniToolProviderCapabilities.ProvidesFields(providerType);
            Type[] snapshotTypes = MiniToolProviderCapabilities.GetSnapshotTypes(providerType);
            bool providesTypedSnapshot = MiniToolProviderCapabilities.ProvidesTypedSnapshot(providerType);
            Type[] streamEventTypes = MiniToolProviderCapabilities.GetStreamEventTypes(providerType);
            bool providesEventStream = MiniToolProviderCapabilities.ProvidesEventStream(providerType);
            Dictionary<Type, int> viewCounts = FindSnapshotViewCounts(prefab);
            Dictionary<Type, int> streamViewCounts = FindStreamViewCounts(prefab);
            bool hasCustomPresentation = HasCustomPresentation(prefab);

            if (viewCounts.Count == 0 && streamViewCounts.Count == 0)
            {
                if (hasCustomPresentation)
                    return;

                bool hasGenericFieldSurface = prefab.GetComponentInChildren<Text>(true) != null || prefab.GetComponentInChildren<TMP_Text>(true) != null;
                if ((providesTypedSnapshot || providesEventStream) && !providesFields)
                {
                    errors.Add(prefix + $"Debug Host prefab '{prefab.name}' has no compatible snapshot or stream view for the provider's typed data.");
                    return;
                }

                if (providesTypedSnapshot || providesEventStream)
                    warnings.Add(prefix + $"Debug Host prefab '{prefab.name}' has no typed snapshot or stream view. Its generic field presentation will be used.");

                if (providesFields && !hasGenericFieldSurface)
                    errors.Add(prefix + $"Debug Host prefab '{prefab.name}' has no Text, TMP_Text, typed snapshot view, or custom presentation.");

                return;
            }

            if (viewCounts.Count > 0 && snapshotTypes.Length == 0)
                errors.Add(prefix + $"Debug Host prefab '{prefab.name}' contains typed snapshot views, but provider '{providerType.FullName}' does not declare IMiniToolSnapshotProvider<TSnapshot>.");

            foreach (Type snapshotType in snapshotTypes)
            {
                viewCounts.TryGetValue(snapshotType, out int matchingViews);
                if (matchingViews == 0)
                    errors.Add(prefix + $"Debug Host prefab '{prefab.name}' is missing IMiniToolSnapshotView<{GetTypeName(snapshotType)}>.");
                else if (matchingViews > 1)
                    errors.Add(prefix + $"Debug Host prefab '{prefab.name}' contains {matchingViews} IMiniToolSnapshotView<{GetTypeName(snapshotType)}> components. Only one is allowed.");
            }

            if (streamViewCounts.Count > 0 && streamEventTypes.Length == 0)
                errors.Add(prefix + $"Debug Host prefab '{prefab.name}' contains typed stream views, but provider '{providerType.FullName}' does not declare IMiniToolStreamProvider<TEvent>.");

            foreach (Type eventType in streamEventTypes)
            {
                streamViewCounts.TryGetValue(eventType, out int matchingViews);
                if (matchingViews == 0)
                    errors.Add(prefix + $"Debug Host prefab '{prefab.name}' is missing IMiniToolStreamView<{GetTypeName(eventType)}>.");
                else if (matchingViews > 1)
                    errors.Add(prefix + $"Debug Host prefab '{prefab.name}' contains {matchingViews} IMiniToolStreamView<{GetTypeName(eventType)}> components. Only one is allowed.");
            }

            foreach (KeyValuePair<Type, int> entry in viewCounts)
            {
                if (Array.IndexOf(snapshotTypes, entry.Key) >= 0)
                    continue;

                warnings.Add(prefix + $"Debug Host prefab '{prefab.name}' contains IMiniToolSnapshotView<{GetTypeName(entry.Key)}>, but the provider does not declare that snapshot type.");
            }

            foreach (KeyValuePair<Type, int> entry in streamViewCounts)
            {
                if (Array.IndexOf(streamEventTypes, entry.Key) >= 0)
                    continue;

                warnings.Add(prefix + $"Debug Host prefab '{prefab.name}' contains IMiniToolStreamView<{GetTypeName(entry.Key)}>, but the provider does not declare that event type.");
            }
        }

        private static Dictionary<Type, int> FindSnapshotViewCounts(GameObject prefab)
        {
            var result = new Dictionary<Type, int>();
            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                foreach (Type implementedInterface in behaviour.GetType().GetInterfaces())
                {
                    if (!implementedInterface.IsGenericType || implementedInterface.GetGenericTypeDefinition() != SnapshotViewType)
                        continue;

                    Type snapshotType = implementedInterface.GetGenericArguments()[0];
                    result.TryGetValue(snapshotType, out int count);
                    result[snapshotType] = count + 1;
                }
            }

            return result;
        }

        private static Dictionary<Type, int> FindStreamViewCounts(GameObject prefab)
        {
            var result = new Dictionary<Type, int>();
            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                foreach (Type implementedInterface in behaviour.GetType().GetInterfaces())
                {
                    if (!implementedInterface.IsGenericType || implementedInterface.GetGenericTypeDefinition() != StreamViewType)
                        continue;

                    Type eventType = implementedInterface.GetGenericArguments()[0];
                    result.TryGetValue(eventType, out int count);
                    result[eventType] = count + 1;
                }
            }

            return result;
        }

        private static bool HasCustomPresentation(GameObject prefab)
        {
            foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is IRemoteMiniToolPresentation)
                    return true;
            }

            return false;
        }

        private static string GetTypeName(Type type) => type?.FullName ?? type?.Name ?? "<unknown>";
    }
}

using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using HP.Utilities.RemoteDevUtilities.MiniTools;
using HP.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    internal sealed class MiniToolRegistration
    {
        internal MiniToolRegistration(MiniToolDefinition definition, string assetPath)
        {
            Definition = definition;
            AssetPath = assetPath;
            Descriptor = definition.CreateDescriptor();
        }

        internal MiniToolDefinition Definition { get; }
        internal string AssetPath { get; }
        internal RemoteMiniToolDescriptor Descriptor { get; }
        internal bool IsProjectOwned => AssetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);

        internal GameObject LoadDebugHostPrefab()
        {
            string guid = Definition.DebugHostPrefabGuid;
            if (string.IsNullOrWhiteSpace(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }

    /// <summary>
    /// Single Editor registry for definitions owned by the package, project, or
    /// any other installed package.
    /// </summary>
    [InitializeOnLoad]
    internal static class MiniToolRegistry
    {
        private static MiniToolRegistration[] _registrations;
        private static string[] _validationErrors;
        private static string[] _validationWarnings;

        static MiniToolRegistry()
        {
            EditorApplication.projectChanged += Invalidate;
            AssemblyReloadEvents.afterAssemblyReload += Invalidate;
            EnsureLoaded();
        }

        internal static event Action Changed;

        internal static IReadOnlyList<MiniToolRegistration> Registrations
        {
            get
            {
                EnsureLoaded();
                return _registrations;
            }
        }

        internal static IReadOnlyList<string> ValidationErrors
        {
            get
            {
                EnsureLoaded();
                return _validationErrors;
            }
        }

        internal static IReadOnlyList<string> ValidationWarnings
        {
            get
            {
                EnsureLoaded();
                return _validationWarnings;
            }
        }

        internal static MiniToolDefinition[] GetDefinitions()
        {
            EnsureLoaded();
            var definitions = new MiniToolDefinition[_registrations.Length];
            for (int i = 0; i < definitions.Length; i++)
                definitions[i] = _registrations[i].Definition;
            return definitions;
        }

        internal static RemoteMiniToolDescriptor[] GetDescriptors()
        {
            EnsureLoaded();
            var descriptors = new RemoteMiniToolDescriptor[_registrations.Length];
            for (int i = 0; i < descriptors.Length; i++)
                descriptors[i] = _registrations[i].Descriptor;
            return descriptors;
        }

        internal static bool TryGet(string toolId, out MiniToolRegistration registration)
        {
            EnsureLoaded();
            foreach (MiniToolRegistration candidate in _registrations)
            {
                if (!string.Equals(candidate.Descriptor.Id, toolId, StringComparison.OrdinalIgnoreCase))
                    continue;

                registration = candidate;
                return true;
            }

            registration = null;
            return false;
        }

        internal static bool TryGetDebugHostPrefab(string toolId, out GameObject prefab)
        {
            if (TryGet(toolId, out MiniToolRegistration registration))
            {
                prefab = registration.LoadDebugHostPrefab();
                return prefab != null;
            }

            prefab = null;
            return false;
        }

        internal static bool TryCreateCommandBinding(string toolId, out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (!TryGet(toolId, out MiniToolRegistration registration))
                return false;

            return RemoteMiniToolCommandManifestResolver.TryCreateBinding(registration.Descriptor, out binding);
        }

        internal static void Invalidate()
        {
            _registrations = null;
            _validationErrors = null;
            _validationWarnings = null;
            EnsureLoaded();
            Changed?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (_registrations != null && _validationErrors != null && _validationWarnings != null)
                return;

            var registrations = new List<MiniToolRegistration>();
            var errors = new List<string>();
            var warnings = new List<string>();
            var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(MiniToolDefinition)}");
            var paths = new List<string>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                MiniToolDefinition definition = AssetDatabase.LoadAssetAtPath<MiniToolDefinition>(path);
                if (definition == null)
                    continue;

                if (IsEditorOnlyPath(path))
                {
                    errors.Add($"{path}: Mini Tool Definitions must be stored outside an Editor folder so they can be baked into a Player.");
                    continue;
                }

                if (!MiniToolProviderReferenceResolver.TrySynchronize(definition, path, out string providerError, out string providerWarning))
                {
                    errors.Add($"{path}: {providerError}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(providerWarning))
                    warnings.Add($"{path}: {providerWarning}");

                if (!definition.TryValidate(out string error))
                {
                    errors.Add($"{path}: {error}");
                    continue;
                }

                int presentationErrorCount = errors.Count;
                MiniToolRegistrationValidator.Validate(definition, path, errors, warnings);
                if (errors.Count > presentationErrorCount)
                    continue;

                if (ids.TryGetValue(definition.ToolId, out string existingPath))
                {
                    errors.Add($"Duplicate mini-tool ID '{definition.ToolId}' in '{existingPath}' and '{path}'.");
                    continue;
                }

                ids.Add(definition.ToolId, path);
                string commandName = definition.CommandName;
                if (!string.IsNullOrWhiteSpace(commandName))
                {
                    if (commands.TryGetValue(commandName, out string commandOwner))
                    {
                        errors.Add($"Command '{commandName}' is assigned by both '{commandOwner}' and '{path}'.");
                        continue;
                    }

                    commands.Add(commandName, path);
                }

                registrations.Add(new MiniToolRegistration(definition, path));
            }

            registrations.Sort((left, right) => string.Compare(left.Descriptor.DisplayName, right.Descriptor.DisplayName, StringComparison.OrdinalIgnoreCase));
            _registrations = registrations.ToArray();
            _validationErrors = errors.ToArray();
            _validationWarnings = warnings.ToArray();
            MiniToolRuntimeRegistry.SetEditorDefinitions(GetRegistrationDefinitions(_registrations));
        }

        private static bool IsEditorOnlyPath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            string[] segments = normalized.Split('/');
            foreach (string segment in segments)
            {
                if (string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static MiniToolDefinition[] GetRegistrationDefinitions(IReadOnlyList<MiniToolRegistration> registrations)
        {
            var definitions = new MiniToolDefinition[registrations.Count];
            for (int i = 0; i < registrations.Count; i++)
                definitions[i] = registrations[i].Definition;
            return definitions;
        }
    }
}

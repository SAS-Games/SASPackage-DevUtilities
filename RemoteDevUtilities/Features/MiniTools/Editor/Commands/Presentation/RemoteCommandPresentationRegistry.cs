using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    /// <summary>
    /// Resolves definition, project override, and advanced code mappings from
    /// target commands to Editor mini-tool presentations.
    /// </summary>
    public static class RemoteCommandPresentationRegistry
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<string, RemoteCommandPresentationBinding> RegisteredBindings = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, RemoteCommandPresentationBinding> _definitionDefaults;
        private static Dictionary<string, RemoteCommandPresentationBinding> _resolvedBindings;

        static RemoteCommandPresentationRegistry()
        {
            EditorApplication.projectChanged += InvalidateDefinitions;
            MiniToolRegistry.Changed += InvalidateDefinitions;
            RemoteMiniToolCommandSettings.Changed += InvalidateDefinitions;
        }

        public static event Action Changed;

        /// <summary>
        /// Registers a command presentation. Pass replaceExisting to intentionally override a
        /// package or project registration with the same command name.
        /// </summary>
        public static bool Register(RemoteCommandPresentationBinding binding, bool replaceExisting = false)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            lock (Sync)
            {
                if (!replaceExisting && (RegisteredBindings.ContainsKey(binding.CommandName) || ResolvedBindings.ContainsKey(binding.CommandName)))
                    return false;
                RegisteredBindings[binding.CommandName] = binding;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Removes an advanced code registration. A catalog mapping with the same command, when
        /// present, becomes active again.
        /// </summary>
        public static bool Unregister(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                return false;

            bool removed;
            lock (Sync)
                removed = RegisteredBindings.Remove(commandName.Trim());

            if (removed)
                Changed?.Invoke();
            return removed;
        }

        public static bool TryGet(string commandName, out RemoteCommandPresentationBinding binding)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                binding = null;
                return false;
            }

            lock (Sync)
            {
                string normalized = commandName.Trim();
                return RegisteredBindings.TryGetValue(normalized, out binding) || ResolvedBindings.TryGetValue(normalized, out binding);
            }
        }

        internal static bool TryGetAdvancedRegistration(string commandName, out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(commandName))
                return false;

            lock (Sync)
            {
                return RegisteredBindings.TryGetValue(commandName.Trim(), out binding);
            }
        }

        internal static bool TryGetProjectOverride(string commandName, out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(commandName))
                return false;

            string normalizedCommandName = commandName.Trim();
            foreach (RemoteMiniToolCommandOverride commandOverride in RemoteMiniToolCommandSettings.instance.Configuration.Overrides)
            {
                if (commandOverride == null || string.IsNullOrWhiteSpace(commandOverride.CommandName) || !string.Equals(commandOverride.CommandName, normalizedCommandName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    binding = new RemoteCommandPresentationBinding(commandOverride.CommandName, commandOverride.ToolId, commandOverride.Routing);
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            return false;
        }

        internal static bool HasProjectOverrideForMiniTool(string miniToolId)
        {
            return RemoteMiniToolCommandSettings.instance.Configuration.TryGet(miniToolId, out _);
        }

        internal static bool TryGetDefinitionBinding(string commandName, out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(commandName))
                return false;

            lock (Sync)
            {
                return ResolvedBindings.TryGetValue(commandName.Trim(), out binding);
            }
        }

        public static RemoteCommandPresentationBinding[] GetRegistrations()
        {
            lock (Sync)
            {
                var combined = new Dictionary<string, RemoteCommandPresentationBinding>(ResolvedBindings, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, RemoteCommandPresentationBinding> registration in RegisteredBindings)
                {
                    combined[registration.Key] = registration.Value;
                }

                var registrations = new RemoteCommandPresentationBinding[combined.Count];
                combined.Values.CopyTo(registrations, 0);
                Array.Sort(registrations, (left, right) => string.Compare(left.CommandName, right.CommandName, StringComparison.OrdinalIgnoreCase));
                return registrations;
            }
        }

        internal static bool TryGetDefinitionDefaultForMiniTool(string miniToolId, out RemoteCommandPresentationBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(miniToolId))
                return false;

            foreach (RemoteCommandPresentationBinding candidate in DefinitionDefaults.Values)
            {
                if (!string.Equals(candidate.MiniToolId, miniToolId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (binding == null || string.Compare(candidate.CommandName, binding.CommandName, StringComparison.OrdinalIgnoreCase) < 0)
                    binding = candidate;
            }

            return binding != null;
        }

        internal static void ApplyProjectOverrides(IDictionary<string, RemoteCommandPresentationBinding> bindings, IEnumerable<RemoteMiniToolCommandOverride> overrides)
        {
            if (bindings == null)
                throw new ArgumentNullException(nameof(bindings));

            foreach (RemoteMiniToolCommandOverride commandOverride in overrides ?? Array.Empty<RemoteMiniToolCommandOverride>())
            {
                if (commandOverride == null || string.IsNullOrWhiteSpace(commandOverride.ToolId))
                    continue;

                RemoveMiniToolBindings(bindings, commandOverride.ToolId);
                if (string.IsNullOrWhiteSpace(commandOverride.CommandName))
                    continue;

                try
                {
                    var binding = new RemoteCommandPresentationBinding(commandOverride.CommandName, commandOverride.ToolId, commandOverride.Routing);
                    bindings[binding.CommandName] = binding;
                }
                catch (ArgumentException)
                {
                    // Invalid serialized project overrides are ignored. The
                    // configuration window validates all newly entered values.
                }
            }
        }

        private static Dictionary<string, RemoteCommandPresentationBinding> DefinitionDefaults
        {
            get
            {
                if (_definitionDefaults != null)
                    return _definitionDefaults;

                _definitionDefaults = new Dictionary<string, RemoteCommandPresentationBinding>(StringComparer.OrdinalIgnoreCase);
                foreach (MiniToolRegistration registration in MiniToolRegistry.Registrations)
                {
                    if (RemoteMiniToolCommandManifestResolver.TryCreateBinding(registration.Descriptor, out RemoteCommandPresentationBinding binding))
                    {
                        _definitionDefaults[binding.CommandName] = binding;
                    }
                }

                return _definitionDefaults;
            }
        }

        private static Dictionary<string, RemoteCommandPresentationBinding> ResolvedBindings
        {
            get
            {
                if (_resolvedBindings != null)
                    return _resolvedBindings;

                _resolvedBindings = new Dictionary<string, RemoteCommandPresentationBinding>(DefinitionDefaults, StringComparer.OrdinalIgnoreCase);
                ApplyProjectOverrides(_resolvedBindings, RemoteMiniToolCommandSettings.instance.Configuration.Overrides);
                return _resolvedBindings;
            }
        }

        internal static void InvalidateDefinitions()
        {
            lock (Sync)
            {
                _definitionDefaults = null;
                _resolvedBindings = null;
            }

            Changed?.Invoke();
        }

        private static void RemoveMiniToolBindings(IDictionary<string, RemoteCommandPresentationBinding> bindings, string miniToolId)
        {
            var commandNames = new List<string>();
            foreach (KeyValuePair<string, RemoteCommandPresentationBinding> entry in bindings)
            {
                if (string.Equals(entry.Value?.MiniToolId, miniToolId, StringComparison.OrdinalIgnoreCase))
                    commandNames.Add(entry.Key);
            }

            foreach (string commandName in commandNames)
                bindings.Remove(commandName);
        }
    }
}

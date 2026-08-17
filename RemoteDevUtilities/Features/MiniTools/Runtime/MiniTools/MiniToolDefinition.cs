using System;
using System.Reflection;
using HP.Utilities.DeveloperConsole;
using HP.Utilities.RemoteDevUtilities.Protocol.Commands;
using HP.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.MiniTools
{
    /// <summary>
    /// The single registration record shared by the Player, Native Workspace,
    /// Debug Host, and command routing.
    /// </summary>
    [CreateAssetMenu(fileName = "MiniToolDefinition", menuName = "HP/Dev Utilities/Mini Tool")]
    public sealed class MiniToolDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _toolId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, TextArea(2, 5)] private string _description = string.Empty;
        [SerializeField, Min(0.1f)] private float _updateInterval = 1f;

        [SerializeField, Min(0.02f)] private float _streamInterval = 0.1f;

        // The Editor uses the stable script GUID to refresh the runtime type
        // identity after namespace or asmdef changes. Players use only the
        // baked provider type name and never depend on UnityEditor.MonoScript.
        [SerializeField, HideInInspector] private string _providerScriptGuid = string.Empty;
        [SerializeField, HideInInspector] private string _providerTypeName = string.Empty;
        [SerializeField] private ConsoleCommand _command;
        [SerializeField, HideInInspector] private string _commandName = string.Empty;

        [SerializeField] private RemoteCommandRouting _commandRouting = RemoteCommandRouting.ControlEditorToolOnly;

        // A GUID is deliberately stored instead of a GameObject reference so an Editor-only Debug Host prefab is never pulled into a Player build.
        [SerializeField, HideInInspector] private string _debugHostPrefabGuid = string.Empty;
        [SerializeField] private bool _visibleByDefault = true;

        public string ToolId => _toolId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description ?? string.Empty;
        public float UpdateInterval => Mathf.Max(0.1f, _updateInterval);
        public float StreamInterval => Mathf.Max(0.02f, _streamInterval);
        public string ProviderTypeName => _providerTypeName ?? string.Empty;
        public ConsoleCommand Command => _command;
        public string CommandName => _command == null ? string.Empty : string.IsNullOrWhiteSpace(_commandName) ? _command.Name : _commandName.Trim();
        public RemoteCommandRouting CommandRouting => _commandRouting;
        public string DebugHostPrefabGuid => _debugHostPrefabGuid ?? string.Empty;
        public bool VisibleByDefault => _visibleByDefault;

        public bool TryGetProviderType(out Type providerType)
        {
            providerType = string.IsNullOrWhiteSpace(_providerTypeName) ? null : Type.GetType(_providerTypeName, false);
            return providerType != null;
        }

        public RemoteMiniToolDescriptor CreateDescriptor()
        {
            string commandName = CommandName;
            TryGetProviderType(out Type providerType);

            RemoteMiniToolCapabilities capabilities = RemoteMiniToolCapabilities.None;
            if (MiniToolProviderCapabilities.ProvidesFields(providerType))
                capabilities |= RemoteMiniToolCapabilities.NativeWorkspaceFields;
            if (MiniToolProviderCapabilities.ProvidesTypedSnapshot(providerType))
                capabilities |= RemoteMiniToolCapabilities.TypedDebugHostSnapshot;
            if (MiniToolProviderCapabilities.ProvidesEventStream(providerType))
                capabilities |= RemoteMiniToolCapabilities.EventStream;

            return new RemoteMiniToolDescriptor
            {
                Id = _toolId?.Trim() ?? string.Empty,
                DisplayName = DisplayName,
                Description = Description,
                DefaultIntervalSeconds = UpdateInterval,
                DefaultStreamIntervalSeconds = StreamInterval,
                VisibleByDefault = _visibleByDefault,
                Capabilities = capabilities,
                Command = string.IsNullOrWhiteSpace(commandName)
                    ? null
                    : new RemoteMiniToolCommandManifest
                    {
                        Name = commandName.Trim(),
                        SuggestedRouting = _commandRouting
                    }
            };
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(_toolId))
            {
                error = "Tool ID is missing.";
                return false;
            }

            if (_toolId.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
            {
                error = "Tool ID cannot contain whitespace.";
                return false;
            }

            if (!TryGetProviderType(out Type providerType))
            {
                error = "Data Provider is missing or its type cannot be loaded.";
                return false;
            }

            bool supportedProvider = typeof(IMiniToolDataProvider).IsAssignableFrom(providerType);
            if (!providerType.IsClass || providerType.IsAbstract || !supportedProvider)
            {
                error = $"'{providerType.FullName}' is not a concrete mini-tool data provider.";
                return false;
            }

            if (providerType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) == null)
            {
                error = $"'{providerType.FullName}' requires a parameterless constructor.";
                return false;
            }

            if (!MiniToolProviderCapabilities.ProvidesFields(providerType) && !MiniToolProviderCapabilities.ProvidesTypedSnapshot(providerType) && !MiniToolProviderCapabilities.ProvidesEventStream(providerType))
            {
                error = $"'{providerType.FullName}' exposes no mini-tool data. Implement {{nameof(IMiniToolFieldProvider)}} for Native Workspace fields and/or IMiniToolSnapshotProvider<TSnapshot> for typed Debug Host snapshot and/or IMiniToolStreamProvider<TEvent> for incremental events.";
                return false;
            }

            foreach (Type snapshotType in MiniToolProviderCapabilities.GetSnapshotTypes(providerType))
            {
                if (snapshotType.IsSerializable)
                    continue;

                error = $"Snapshot type '{snapshotType.FullName}' must be serializable for the current JSON snapshot transport.";
                return false;
            }

            foreach (Type eventType in MiniToolProviderCapabilities.GetStreamEventTypes(providerType))
            {
                if (eventType.IsSerializable)
                    continue;

                error = $"Stream event type '{eventType.FullName}' must be serializable for the current JSON event transport.";
                return false;
            }

            if (_command != null)
            {
                string commandName = CommandName;
                if (string.IsNullOrWhiteSpace(commandName) || commandName.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
                {
                    error = "Command must expose a non-empty name without whitespace.";
                    return false;
                }

                if (!string.Equals(commandName, _command.Name, StringComparison.OrdinalIgnoreCase) && !ContainsCommandAction(_command.Presets, commandName))
                {
                    error = $"Command action '{commandName}' is not declared by '{_command.Name}'.";
                    return false;
                }
            }

            if (!Enum.IsDefined(typeof(RemoteCommandRouting), _commandRouting))
            {
                error = "Command routing contains an unsupported value.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ContainsCommandAction(string[] actions, string commandName)
        {
            foreach (string action in actions ?? Array.Empty<string>())
            {
                if (string.Equals(action, commandName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_toolId))
                _toolId = $"mini-tool.{Guid.NewGuid():N}";
            else
                _toolId = _toolId.Trim();

            _displayName = _displayName?.Trim() ?? string.Empty;
            _description = _description?.Trim() ?? string.Empty;
            _providerScriptGuid = _providerScriptGuid?.Trim() ?? string.Empty;
            _providerTypeName = _providerTypeName?.Trim() ?? string.Empty;
            _commandName = _commandName?.Trim() ?? string.Empty;
            _debugHostPrefabGuid = _debugHostPrefabGuid?.Trim() ?? string.Empty;
            _updateInterval = Mathf.Max(0.1f, _updateInterval);
            _streamInterval = Mathf.Max(0.02f, _streamInterval);
        }
    }
}

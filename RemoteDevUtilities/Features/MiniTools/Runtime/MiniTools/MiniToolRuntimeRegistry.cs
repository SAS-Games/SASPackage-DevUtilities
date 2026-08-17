using System;
using System.Collections.Generic;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;
using RuntimeConsole = SAS.Utilities.DeveloperConsole.DeveloperConsole;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools
{
    internal sealed class MiniToolProviderRegistration : IDisposable
    {
        private readonly IMiniToolDataProvider _provider;
        private readonly IRemoteMiniToolSnapshotCapture _snapshotCapture;
        private readonly IRemoteMiniToolStreamCapture _streamCapture;
        private readonly IMiniToolFieldProvider _fieldProvider;
        private readonly MiniToolDataProvider _actionProvider;
        private long _streamSequence;

        internal MiniToolProviderRegistration(RemoteMiniToolDescriptor descriptor, IMiniToolDataProvider provider)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _fieldProvider = provider as IMiniToolFieldProvider;
            _actionProvider = provider as MiniToolDataProvider;
            Descriptor.Actions = GetValidActions(_actionProvider);
            if (Descriptor.Actions.Length > 0)
                Descriptor.Capabilities |= RemoteMiniToolCapabilities.Actions;

            _snapshotCapture = RemoteMiniToolSnapshotCaptureFactory.Create(provider, out string snapshotCaptureError);
            if (!string.IsNullOrWhiteSpace(snapshotCaptureError))
            {
                Debug.LogWarning($"Mini-tool '{Descriptor.Id}' cannot stream its typed snapshot: " + snapshotCaptureError);
            }

            _streamCapture = RemoteMiniToolStreamCaptureFactory.Create(provider, out string streamCaptureError);
            if (!string.IsNullOrWhiteSpace(streamCaptureError))
            {
                Debug.LogWarning($"Mini-tool '{Descriptor.Id}' cannot stream its incremental events: " + streamCaptureError);
            }
        }

        internal RemoteMiniToolDescriptor Descriptor { get; }
        internal bool SupportsEventStream => _streamCapture != null;
        internal bool SupportsActions => Descriptor.Actions.Length > 0;

        internal void Start()
        {
            _streamSequence = 0;
            _provider.Start();
        }

        internal void Stop() => _provider.Stop();
        internal void Tick() => _provider.Tick();

        internal bool TryExecuteAction(string actionId, out string error)
        {
            if (_actionProvider == null || !ContainsAction(actionId))
            {
                error = "The requested action is not available for this mini-tool.";
                return false;
            }

            try
            {
                bool success = _actionProvider.TryExecuteAction(actionId, out error);
                if (!success && string.IsNullOrWhiteSpace(error))
                    error = "The mini-tool rejected the requested action.";
                return success;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }
        }

        internal RemoteMiniToolSample Capture(RemoteMiniToolDataChannels dataChannels)
        {
            var sample = new RemoteMiniToolSample
            {
                ToolId = Descriptor.Id,
                Timestamp = Time.realtimeSinceStartupAsDouble,
                Frame = Time.frameCount
            };

            if ((dataChannels & RemoteMiniToolDataChannels.TypedSnapshot) != 0 && _snapshotCapture != null && _snapshotCapture.TryCapture(out string snapshotTypeName, out string snapshotJson))
            {
                sample.SnapshotTypeName = snapshotTypeName;
                sample.SnapshotJson = snapshotJson;
            }

            if ((dataChannels & RemoteMiniToolDataChannels.NativeWorkspaceFields) != 0)
                sample.Fields = _fieldProvider?.CaptureFields() ?? Array.Empty<RemoteMiniToolField>();

            return sample;
        }

        internal RemoteMiniToolStreamBatch CaptureStream()
        {
            if (_streamCapture == null || !_streamCapture.TryCapture(out string eventTypeName, out string eventsJson, out int droppedEventCount))
            {
                return null;
            }

            return new RemoteMiniToolStreamBatch
            {
                ToolId = Descriptor.Id,
                Timestamp = Time.realtimeSinceStartupAsDouble,
                Frame = Time.frameCount,
                Sequence = ++_streamSequence,
                DroppedEventCount = Mathf.Max(0, droppedEventCount),
                EventTypeName = eventTypeName,
                EventsJson = eventsJson
            };
        }

        public void Dispose()
        {
            if (_provider is IDisposable disposable)
                disposable.Dispose();
        }

        private bool ContainsAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            foreach (RemoteMiniToolActionDescriptor action in Descriptor.Actions)
            {
                if (string.Equals(action.Id, actionId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static RemoteMiniToolActionDescriptor[] GetValidActions(MiniToolDataProvider provider)
        {
            if (provider == null)
                return Array.Empty<RemoteMiniToolActionDescriptor>();

            RemoteMiniToolActionDescriptor[] declared;
            try
            {
                declared = provider.GetActions() ?? Array.Empty<RemoteMiniToolActionDescriptor>();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("A mini-tool provider could not describe its actions: " + exception.GetBaseException().Message);
                return Array.Empty<RemoteMiniToolActionDescriptor>();
            }

            var actions = new List<RemoteMiniToolActionDescriptor>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RemoteMiniToolActionDescriptor action in declared)
            {
                string id = action?.Id?.Trim();
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    continue;

                actions.Add(new RemoteMiniToolActionDescriptor
                {
                    Id = id,
                    DisplayName = string.IsNullOrWhiteSpace(action.DisplayName) ? id : action.DisplayName.Trim(),
                    HideInNativeWorkspace = action.HideInNativeWorkspace
                });
            }

            return actions.ToArray();
        }
    }

    /// <summary>
    /// Creates runtime provider instances from the same definitions used by the
    /// Editor.
    /// </summary>
    internal static class MiniToolRuntimeRegistry
    {
        private static MiniToolDefinition[] _editorDefinitions = Array.Empty<MiniToolDefinition>();

        internal static void SetEditorDefinitions(IEnumerable<MiniToolDefinition> definitions)
        {
            _editorDefinitions = CopyDefinitions(definitions);
        }

        internal static List<MiniToolProviderRegistration> CreateRegistrations()
        {
            var registrations = new List<MiniToolProviderRegistration>();
            var toolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (MiniToolDefinition definition in FindDefinitions())
            {
                if (definition == null)
                    continue;

                if (!definition.TryValidate(out string error))
                {
                    Debug.LogWarning($"Mini-tool definition '{definition.name}' was skipped: {error}", definition);
                    continue;
                }

                RemoteMiniToolDescriptor descriptor = definition.CreateDescriptor();
                if (!toolIds.Add(descriptor.Id))
                {
                    Debug.LogWarning($"Duplicate mini-tool ID '{descriptor.Id}' was skipped.", definition);
                    continue;
                }

                if (!TryCreateProvider(definition, out IMiniToolDataProvider provider, out error))
                {
                    Debug.LogWarning($"Mini-tool definition '{definition.name}' was skipped: {error}", definition);
                    toolIds.Remove(descriptor.Id);
                    continue;
                }

                registrations.Add(new MiniToolProviderRegistration(descriptor, provider));
            }

            registrations.Sort((left, right) => string.Compare(left.Descriptor.Id, right.Descriptor.Id, StringComparison.OrdinalIgnoreCase));
            return registrations;
        }

        internal static void RegisterCommands(DeveloperConsole.DeveloperConsole console)
        {
            if (console == null)
                return;

            foreach (MiniToolDefinition definition in FindDefinitions())
            {
                ConsoleCommand command = definition?.Command;
                if (command == null || !definition.TryValidate(out _) || ContainsCommand(console, command.Name))
                    continue;

                console.AddCommand(command);
            }
        }

        private static bool ContainsCommand(DeveloperConsole.DeveloperConsole console, string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                return true;

            foreach (IConsoleCommand command in console.ConsoleCommands)
            {
                if (command != null && string.Equals(command.Name, commandName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static MiniToolDefinition[] FindDefinitions()
        {
            var definitions = new Dictionary<string, MiniToolDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (MiniToolDefinition definition in Resources.FindObjectsOfTypeAll<MiniToolDefinition>())
            {
                AddDefinition(definitions, definition);
            }

            foreach (MiniToolDefinition definition in _editorDefinitions)
                AddDefinition(definitions, definition);

            var result = new MiniToolDefinition[definitions.Count];
            definitions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => string.Compare(left.ToolId, right.ToolId, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static void AddDefinition(IDictionary<string, MiniToolDefinition> definitions, MiniToolDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ToolId) || definitions.ContainsKey(definition.ToolId))
                return;

            definitions.Add(definition.ToolId, definition);
        }

        private static bool TryCreateProvider(MiniToolDefinition definition, out IMiniToolDataProvider provider, out string error)
        {
            provider = null;
            if (!definition.TryGetProviderType(out Type providerType))
            {
                error = "Data Provider type could not be loaded.";
                return false;
            }

            try
            {
                provider = Activator.CreateInstance(providerType, true) as IMiniToolDataProvider;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                return false;
            }

            if (provider != null)
            {
                error = string.Empty;
                return true;
            }

            error = $"'{providerType.FullName}' does not implement a supported provider interface.";
            provider = null;
            return false;
        }

        private static MiniToolDefinition[] CopyDefinitions(IEnumerable<MiniToolDefinition> definitions)
        {
            if (definitions == null)
                return Array.Empty<MiniToolDefinition>();

            var result = new List<MiniToolDefinition>();
            foreach (MiniToolDefinition definition in definitions)
            {
                if (definition != null && !result.Contains(definition))
                    result.Add(definition);
            }

            return result.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    [Serializable]
    internal sealed class RemoteMiniToolKnownCommand
    {
        public string Name;
        public RemoteCommandRouting SuggestedRouting = RemoteCommandRouting.ControlEditorToolOnly;
    }

    [Serializable]
    internal sealed class RemoteMiniToolKnownDescriptor
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public float DefaultIntervalSeconds;
        public float DefaultStreamIntervalSeconds;
        public bool VisibleByDefault = true;
        public int Capabilities;
        public RemoteMiniToolKnownCommand Command;
    }

    [Serializable]
    internal sealed class RemoteMiniToolVisibilityConfiguration
    {
        [SerializeField] private bool _showNewToolsByDefault = true;
        [SerializeField] private List<string> _visibleToolIds = new();
        [SerializeField] private List<string> _hiddenToolIds = new();
        [SerializeField] private List<RemoteMiniToolKnownDescriptor> _knownTools = new();

        internal bool ShowNewToolsByDefault => _showNewToolsByDefault;
        internal IReadOnlyList<RemoteMiniToolKnownDescriptor> KnownTools => _knownTools;

        internal bool IsVisible(string toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return false;
            if (Contains(_hiddenToolIds, toolId))
                return false;
            if (Contains(_visibleToolIds, toolId))
                return true;
            RemoteMiniToolKnownDescriptor descriptor = FindKnownDescriptor(toolId);
            return _showNewToolsByDefault && (descriptor?.VisibleByDefault ?? true);
        }

        internal bool SetVisible(string toolId, bool visible)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return false;
            bool alreadyExplicit = visible ? Contains(_visibleToolIds, toolId) : Contains(_hiddenToolIds, toolId);
            if (alreadyExplicit)
                return false;
            _visibleToolIds.RemoveAll(value => string.Equals(value, toolId, StringComparison.OrdinalIgnoreCase));
            _hiddenToolIds.RemoveAll(value => string.Equals(value, toolId, StringComparison.OrdinalIgnoreCase));
            (visible ? _visibleToolIds : _hiddenToolIds).Add(toolId);
            return true;
        }

        internal bool SetShowNewToolsByDefault(bool show)
        {
            if (_showNewToolsByDefault == show)
                return false;
            _showNewToolsByDefault = show;
            return true;
        }

        internal bool ShowAll()
        {
            var requiredVisibleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RemoteMiniToolKnownDescriptor descriptor in _knownTools)
            {
                if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Id) && !descriptor.VisibleByDefault)
                    requiredVisibleIds.Add(descriptor.Id);
            }
            bool changed = !_showNewToolsByDefault || _hiddenToolIds.Count > 0 || !requiredVisibleIds.SetEquals(_visibleToolIds);
            _showNewToolsByDefault = true;
            _hiddenToolIds.Clear();
            _visibleToolIds.Clear();
            _visibleToolIds.AddRange(requiredVisibleIds);
            return changed;
        }

        internal bool HideAll()
        {
            bool changed = _showNewToolsByDefault || _visibleToolIds.Count > 0 || _hiddenToolIds.Count > 0;
            _showNewToolsByDefault = false;
            _visibleToolIds.Clear();
            _hiddenToolIds.Clear();
            return changed;
        }

        internal bool ResetOverrides()
        {
            if (_visibleToolIds.Count == 0 && _hiddenToolIds.Count == 0)
                return false;
            _visibleToolIds.Clear();
            _hiddenToolIds.Clear();
            return true;
        }

        internal bool RegisterCatalog(IEnumerable<RemoteMiniToolKnownDescriptor> descriptors)
        {
            bool changed = false;
            foreach (RemoteMiniToolKnownDescriptor descriptor in descriptors ?? Array.Empty<RemoteMiniToolKnownDescriptor>())
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                    continue;
                int index = FindKnownTool(descriptor.Id);
                if (index < 0)
                {
                    _knownTools.Add(Clone(descriptor));
                    changed = true;
                    continue;
                }
                if (DescriptorEquals(_knownTools[index], descriptor))
                    continue;
                _knownTools[index] = Clone(descriptor);
                changed = true;
            }
            return changed;
        }

        internal bool Forget(string toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return false;
            bool changed = _knownTools.RemoveAll(descriptor =>
                string.Equals(descriptor?.Id, toolId, StringComparison.OrdinalIgnoreCase)) > 0;
            changed |= _visibleToolIds.RemoveAll(value =>
                string.Equals(value, toolId, StringComparison.OrdinalIgnoreCase)) > 0;
            changed |= _hiddenToolIds.RemoveAll(value =>
                string.Equals(value, toolId, StringComparison.OrdinalIgnoreCase)) > 0;
            return changed;
        }

        private int FindKnownTool(string toolId)
        {
            for (int i = 0; i < _knownTools.Count; i++)
            {
                if (string.Equals(_knownTools[i]?.Id, toolId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private RemoteMiniToolKnownDescriptor FindKnownDescriptor(string toolId)
        {
            int index = FindKnownTool(toolId);
            return index < 0 ? null : _knownTools[index];
        }

        private static RemoteMiniToolKnownDescriptor Clone(RemoteMiniToolKnownDescriptor descriptor) => new()
        {
            Id = descriptor.Id,
            DisplayName = descriptor.DisplayName,
            Description = descriptor.Description,
            DefaultIntervalSeconds = descriptor.DefaultIntervalSeconds,
            DefaultStreamIntervalSeconds = descriptor.DefaultStreamIntervalSeconds,
            VisibleByDefault = descriptor.VisibleByDefault,
            Capabilities = descriptor.Capabilities,
            Command = descriptor.Command == null ? null : new RemoteMiniToolKnownCommand
                {
                    Name = descriptor.Command.Name,
                    SuggestedRouting = descriptor.Command.SuggestedRouting
                }
        };

        private static bool DescriptorEquals(RemoteMiniToolKnownDescriptor left, RemoteMiniToolKnownDescriptor right) =>
            string.Equals(left?.Id, right?.Id, StringComparison.Ordinal) &&
            string.Equals(left?.DisplayName, right?.DisplayName, StringComparison.Ordinal) &&
            string.Equals(left?.Description, right?.Description, StringComparison.Ordinal) &&
            Mathf.Approximately(left?.DefaultIntervalSeconds ?? 0f, right?.DefaultIntervalSeconds ?? 0f) &&
            Mathf.Approximately(left?.DefaultStreamIntervalSeconds ?? 0f,
                right?.DefaultStreamIntervalSeconds ?? 0f) &&
            (left?.VisibleByDefault ?? true) == (right?.VisibleByDefault ?? true) &&
            (left?.Capabilities ?? 0) == (right?.Capabilities ?? 0) &&
            string.Equals(left?.Command?.Name, right?.Command?.Name, StringComparison.Ordinal) &&
            (left?.Command?.SuggestedRouting ?? RemoteCommandRouting.ControlEditorToolOnly) ==
            (right?.Command?.SuggestedRouting ?? RemoteCommandRouting.ControlEditorToolOnly);

        private static bool Contains(List<string> values, string toolId)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], toolId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}

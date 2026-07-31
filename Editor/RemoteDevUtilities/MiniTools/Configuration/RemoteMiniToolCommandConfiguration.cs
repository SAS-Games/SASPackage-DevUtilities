using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    [Serializable]
    internal sealed class RemoteMiniToolCommandOverride
    {
        [SerializeField] private string _toolId = string.Empty;
        [SerializeField] private string _commandName = string.Empty;
        [SerializeField] private RemoteCommandRouting _routing = RemoteCommandRouting.ControlEditorToolOnly;

        private RemoteMiniToolCommandOverride()
        {
        }

        internal RemoteMiniToolCommandOverride(string toolId, string commandName, RemoteCommandRouting routing)
        {
            _toolId = toolId;
            _commandName = commandName;
            _routing = routing;
        }

        internal string ToolId => _toolId;
        internal string CommandName => _commandName;
        internal RemoteCommandRouting Routing => _routing;

        internal void Set(string commandName, RemoteCommandRouting routing)
        {
            _commandName = commandName;
            _routing = routing;
        }
    }

    [Serializable]
    internal sealed class RemoteMiniToolCommandConfiguration
    {
        [SerializeField] private List<RemoteMiniToolCommandOverride> _overrides = new();

        internal IReadOnlyList<RemoteMiniToolCommandOverride> Overrides => Entries;

        internal bool TryGet(string toolId, out RemoteMiniToolCommandOverride commandOverride)
        {
            int index = Find(toolId);
            if (index < 0)
            {
                commandOverride = null;
                return false;
            }

            commandOverride = Entries[index];
            return commandOverride != null;
        }

        internal bool Set(string toolId, string commandName, RemoteCommandRouting routing)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return false;

            string normalizedToolId = toolId.Trim();
            string normalizedCommandName = commandName?.Trim() ?? string.Empty;
            int index = Find(normalizedToolId);
            if (index >= 0)
            {
                RemoteMiniToolCommandOverride existing = Entries[index];
                if (string.Equals(existing.CommandName, normalizedCommandName, StringComparison.Ordinal) && existing.Routing == routing)
                    return false;

                existing.Set(normalizedCommandName, routing);
                return true;
            }

            Entries.Add(new RemoteMiniToolCommandOverride(normalizedToolId, normalizedCommandName, routing));
            return true;
        }

        internal bool Clear(string toolId)
        {
            int index = Find(toolId);
            if (index < 0)
                return false;

            Entries.RemoveAt(index);
            return true;
        }

        private int Find(string toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return -1;

            for (int i = 0; i < Entries.Count; i++)
            {
                if (string.Equals(Entries[i]?.ToolId, toolId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private List<RemoteMiniToolCommandOverride> Entries => _overrides ??= new List<RemoteMiniToolCommandOverride>();
    }
}

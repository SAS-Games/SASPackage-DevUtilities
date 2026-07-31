using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration
{
    [Serializable]
    internal sealed class RemoteMiniToolPresentationOverride
    {
        [SerializeField] private string _toolId = string.Empty;
        [SerializeField] private string _prefabGuid = string.Empty;

        private RemoteMiniToolPresentationOverride()
        {
        }

        internal RemoteMiniToolPresentationOverride(string toolId, string prefabGuid)
        {
            _toolId = toolId;
            _prefabGuid = prefabGuid;
        }

        internal string ToolId => _toolId;
        internal string PrefabGuid => _prefabGuid;

        internal void SetPrefabGuid(string prefabGuid)
        {
            _prefabGuid = prefabGuid;
        }
    }

    [Serializable]
    internal sealed class RemoteMiniToolPresentationConfiguration
    {
        [SerializeField] private List<RemoteMiniToolPresentationOverride> _overrides = new();

        internal IReadOnlyList<RemoteMiniToolPresentationOverride> Overrides => Entries;

        internal bool TryGetPrefabGuid(string toolId, out string prefabGuid)
        {
            int index = Find(toolId);
            if (index < 0)
            {
                prefabGuid = string.Empty;
                return false;
            }

            prefabGuid = Entries[index].PrefabGuid;
            return true;
        }

        internal bool SetPrefabGuid(string toolId, string prefabGuid)
        {
            if (string.IsNullOrWhiteSpace(toolId) || string.IsNullOrWhiteSpace(prefabGuid))
                return false;

            int index = Find(toolId);
            if (index >= 0)
            {
                if (string.Equals(Entries[index].PrefabGuid, prefabGuid, StringComparison.OrdinalIgnoreCase))
                    return false;

                Entries[index].SetPrefabGuid(prefabGuid);
                return true;
            }

            Entries.Add(new RemoteMiniToolPresentationOverride(toolId, prefabGuid));
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

        private List<RemoteMiniToolPresentationOverride> Entries => _overrides ??= new List<RemoteMiniToolPresentationOverride>();
    }
}

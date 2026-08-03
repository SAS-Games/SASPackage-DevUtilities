using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost.MiniTools
{
    internal sealed class RemoteMiniToolPrefabPresenter : IDisposable
    {
        private readonly RemoteDevUtilitiesClient _client;
        private RemoteMiniToolPrefabDefinition[] _definitions;
        private readonly Dictionary<string, RemoteMiniToolPrefabView> _views = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RemoteMiniToolSample> _presentedSamples = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedViews = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<RemoteMiniToolStreamBatch> _pendingStreamBatches = new();

        public RemoteMiniToolPrefabPresenter(RemoteDevUtilitiesClient client)
        {
            _client = client;
            _definitions = RemoteMiniToolPrefabDefinitions.Discover();
            _client.StateChanged += Refresh;
            RemoteMiniToolPresentationSettings.Changed += ReloadPresentationDefinitions;
            MiniToolRegistry.Changed += ReloadPresentationDefinitions;
            Refresh();
        }

        public void Dispose()
        {
            _client.StateChanged -= Refresh;
            RemoteMiniToolPresentationSettings.Changed -= ReloadPresentationDefinitions;
            MiniToolRegistry.Changed -= ReloadPresentationDefinitions;
            foreach (RemoteMiniToolPrefabView view in _views.Values)
                view.Dispose();
            _views.Clear();
            _presentedSamples.Clear();
            _failedViews.Clear();
        }

        private void ReloadPresentationDefinitions()
        {
            foreach (RemoteMiniToolPrefabView view in _views.Values)
                view.Dispose();
            _views.Clear();
            _presentedSamples.Clear();
            _failedViews.Clear();
            _definitions = RemoteMiniToolPrefabDefinitions.Discover();
            Refresh();
        }

        private void Refresh()
        {
            bool repaintRequested = false;
            RemoteMiniToolPrefabDefinition[] activeDefinitions = IncludeTargetTools(_definitions);
            RemoveInactiveViews(activeDefinitions);
            for (int i = 0; i < activeDefinitions.Length; i++)
            {
                RemoteMiniToolPrefabDefinition definition = activeDefinitions[i];
                bool subscribed = _client.MiniTools.IsSubscriptionRequested(definition.ToolId, RemoteMiniToolSubscriptionOwner.DebugHost);
                if (!subscribed)
                {
                    if (_views.TryGetValue(definition.ToolId, out RemoteMiniToolPrefabView oldView))
                    {
                        oldView.Dispose();
                        _views.Remove(definition.ToolId);
                    }

                    _presentedSamples.Remove(definition.ToolId);
                    _failedViews.Remove(definition.ToolId);
                    continue;
                }

                if (_failedViews.Contains(definition.ToolId))
                    continue;

                if (!_views.TryGetValue(definition.ToolId, out RemoteMiniToolPrefabView view))
                {
                    view = new RemoteMiniToolPrefabView(definition, i, actionId => _client.MiniTools.ExecuteAction(definition.ToolId, actionId));
                    if (!view.IsValid)
                    {
                        Debug.LogWarning($"Remote mini-tool '{definition.ToolId}' could not create its " + $"Debug Host view. {view.FailureReason}");
                        view.Dispose();
                        _failedViews.Add(definition.ToolId);
                        continue;
                    }

                    _views.Add(definition.ToolId, view);
                }

                if (_client.MiniTools.Samples.TryGetValue(definition.ToolId, out RemoteMiniToolSample sample) && (!_presentedSamples.TryGetValue(definition.ToolId, out RemoteMiniToolSample presentedSample) || !ReferenceEquals(presentedSample, sample)))
                {
                    _client.MiniTools.TryGetTool(definition.ToolId, out RemoteMiniToolDescriptor descriptor);
                    view.Update(descriptor, sample);
                    _presentedSamples[definition.ToolId] = sample;
                    repaintRequested = true;
                }

                _pendingStreamBatches.Clear();
                _client.MiniTools.DrainStreamBatches(definition.ToolId, _pendingStreamBatches);
                foreach (RemoteMiniToolStreamBatch batch in _pendingStreamBatches)
                {
                    view.ApplyStream(batch);
                    repaintRequested = true;
                }
            }

            if (repaintRequested)
                RemoteDebugHostRepaintScheduler.Request();
        }

        private void RemoveInactiveViews(IReadOnlyList<RemoteMiniToolPrefabDefinition> definitions)
        {
            var activeToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RemoteMiniToolPrefabDefinition definition in definitions)
                activeToolIds.Add(definition.ToolId);

            var staleToolIds = new List<string>();
            foreach (KeyValuePair<string, RemoteMiniToolPrefabView> entry in _views)
            {
                if (!activeToolIds.Contains(entry.Key) || !_client.MiniTools.IsSubscriptionRequested(entry.Key, RemoteMiniToolSubscriptionOwner.DebugHost))
                    staleToolIds.Add(entry.Key);
            }

            foreach (string toolId in staleToolIds)
            {
                _views[toolId].Dispose();
                _views.Remove(toolId);
                _presentedSamples.Remove(toolId);
                _failedViews.Remove(toolId);
            }

            _failedViews.RemoveWhere(toolId => !activeToolIds.Contains(toolId));
        }

        private RemoteMiniToolPrefabDefinition[] IncludeTargetTools(IReadOnlyList<RemoteMiniToolPrefabDefinition> definitions)
        {
            var combined = new Dictionary<string, RemoteMiniToolPrefabDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (RemoteMiniToolPrefabDefinition definition in definitions)
                combined[definition.ToolId] = definition;

            foreach (RemoteMiniToolDescriptor descriptor in _client.MiniTools.Tools ?? Array.Empty<RemoteMiniToolDescriptor>())
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id) || combined.ContainsKey(descriptor.Id))
                    continue;

                combined.Add(descriptor.Id, new RemoteMiniToolPrefabDefinition(descriptor.Id, string.Empty));
            }

            var result = new RemoteMiniToolPrefabDefinition[combined.Count];
            combined.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => string.Compare(left.ToolId, right.ToolId, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }
}

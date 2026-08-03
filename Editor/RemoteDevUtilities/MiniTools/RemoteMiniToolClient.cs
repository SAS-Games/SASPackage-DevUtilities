using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools
{
    internal enum RemoteMiniToolSubscriptionOwner
    {
        NativeWorkspace,
        DebugHost
    }

    internal sealed class RemoteMiniToolClient : IRemoteEditorFeatureClient
    {
        private sealed class SubscriptionDemand
        {
            public RemoteMiniToolDataChannels DataChannels;
            public float IntervalSeconds;
            public float StreamIntervalSeconds;
        }

        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.MiniToolCatalogResponse,
            RemoteMessageTypes.MiniToolSubscriptionResponse,
            RemoteMessageTypes.MiniToolActionResponse,
            RemoteMessageTypes.MiniToolSample,
            RemoteMessageTypes.MiniToolStreamBatch
        };

        private const int MaximumQueuedStreamBatchesPerTool = 64;
        private readonly IRemoteEditorSession _session;
        private readonly Dictionary<string, RemoteMiniToolSample> _samples = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<RemoteMiniToolStreamBatch>> _streamBatches = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<RemoteMiniToolSubscriptionOwner, SubscriptionDemand>> _subscriptionDemands = new(StringComparer.OrdinalIgnoreCase);

        public RemoteMiniToolClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public RemoteMiniToolDescriptor[] Tools { get; private set; } = Array.Empty<RemoteMiniToolDescriptor>();
        public IReadOnlyDictionary<string, RemoteMiniToolSample> Samples => _samples;
        public string Error { get; private set; }

        public void RequestCatalog()
        {
            _session.Send(RemoteMessageTypes.MiniToolCatalogRequest, new RemoteMiniToolCatalogRequest());
        }

        public bool IsSubscribed(string toolId) => !string.IsNullOrWhiteSpace(toolId) && _subscriptions.Contains(toolId);

        public bool IsSubscriptionRequested(string toolId, RemoteMiniToolSubscriptionOwner owner)
        {
            return !string.IsNullOrWhiteSpace(toolId) && _subscriptionDemands.TryGetValue(toolId, out Dictionary<RemoteMiniToolSubscriptionOwner, SubscriptionDemand> demands) && demands.ContainsKey(owner);
        }

        public bool TryGetTool(string toolId, out RemoteMiniToolDescriptor descriptor)
        {
            if (!string.IsNullOrWhiteSpace(toolId))
            {
                foreach (RemoteMiniToolDescriptor tool in Tools)
                {
                    if (tool != null && string.Equals(tool.Id, toolId, StringComparison.OrdinalIgnoreCase))
                    {
                        descriptor = tool;
                        return true;
                    }
                }
            }

            descriptor = null;
            return false;
        }

        public void SetSubscription(string toolId, RemoteMiniToolSubscriptionOwner owner, bool subscribe, float intervalSeconds, RemoteMiniToolDataChannels dataChannels)
        {
            float streamIntervalSeconds = 0f;
            if (TryGetTool(toolId, out RemoteMiniToolDescriptor descriptor))
                streamIntervalSeconds = descriptor.DefaultStreamIntervalSeconds;

            if (!_subscriptionDemands.TryGetValue(toolId, out Dictionary<RemoteMiniToolSubscriptionOwner, SubscriptionDemand> demands))
            {
                demands = new Dictionary<RemoteMiniToolSubscriptionOwner, SubscriptionDemand>();
                _subscriptionDemands.Add(toolId, demands);
            }

            if (subscribe)
            {
                demands[owner] = new SubscriptionDemand
                {
                    DataChannels = dataChannels,
                    IntervalSeconds = intervalSeconds,
                    StreamIntervalSeconds = streamIntervalSeconds
                };
            }
            else
            {
                demands.Remove(owner);
                if (demands.Count == 0)
                    _subscriptionDemands.Remove(toolId);
            }

            SendAggregatedSubscription(toolId);
        }

        public void ClearSubscriptions(string toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return;

            _subscriptionDemands.Remove(toolId);
            SendAggregatedSubscription(toolId);
        }

        private void SendAggregatedSubscription(string toolId)
        {
            RemoteMiniToolDataChannels channels = RemoteMiniToolDataChannels.None;
            float intervalSeconds = 0f;
            float streamIntervalSeconds = 0f;
            bool subscribe = _subscriptionDemands.TryGetValue(toolId, out Dictionary<RemoteMiniToolSubscriptionOwner, SubscriptionDemand> demands) && demands.Count > 0;
            if (subscribe)
            {
                foreach (SubscriptionDemand demand in demands.Values)
                {
                    channels |= demand.DataChannels;
                    intervalSeconds = MinimumPositive(intervalSeconds, demand.IntervalSeconds);
                    streamIntervalSeconds = MinimumPositive(streamIntervalSeconds, demand.StreamIntervalSeconds);
                }
            }

            if ((channels & RemoteMiniToolDataChannels.EventStream) == 0)
                _streamBatches.Remove(toolId);
            if ((channels & (RemoteMiniToolDataChannels.NativeWorkspaceFields | RemoteMiniToolDataChannels.TypedSnapshot)) == 0)
                _samples.Remove(toolId);

            _session.Send(RemoteMessageTypes.MiniToolSubscriptionRequest, new RemoteMiniToolSubscriptionRequest
            {
                ToolId = toolId,
                Subscribe = subscribe,
                DataChannels = channels,
                IntervalSeconds = intervalSeconds,
                StreamIntervalSeconds = streamIntervalSeconds
            });
        }

        private static float MinimumPositive(float current, float candidate)
        {
            if (candidate <= 0f)
                return current;
            return current <= 0f ? candidate : Math.Min(current, candidate);
        }

        public void ExecuteAction(string toolId, string actionId)
        {
            if (!IsSubscribed(toolId))
            {
                Error = "Start the mini-tool before using its actions.";
                _session.NotifyStateChanged();
                return;
            }

            _session.Send(RemoteMessageTypes.MiniToolActionRequest, new RemoteMiniToolActionRequest
            {
                ToolId = toolId,
                ActionId = actionId
            });
        }

        public void DrainStreamBatches(string toolId, ICollection<RemoteMiniToolStreamBatch> destination)
        {
            if (string.IsNullOrWhiteSpace(toolId) || destination == null || !_streamBatches.TryGetValue(toolId, out Queue<RemoteMiniToolStreamBatch> batches))
            {
                return;
            }

            while (batches.Count > 0)
                destination.Add(batches.Dequeue());
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.MiniToolCatalogResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolCatalogResponse catalog, out string catalogError))
                    {
                        Tools = catalog.Tools ?? Array.Empty<RemoteMiniToolDescriptor>();
                        Error = null;
                    }
                    else
                    {
                        Error = catalogError;
                    }

                    break;
                case RemoteMessageTypes.MiniToolSubscriptionResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolSubscriptionResponse subscription, out string subscriptionError))
                    {
                        if (subscription.Success)
                        {
                            if (subscription.Subscribed)
                            {
                                _subscriptions.Add(subscription.ToolId);
                            }
                            else
                            {
                                _subscriptions.Remove(subscription.ToolId);
                                _samples.Remove(subscription.ToolId);
                                _streamBatches.Remove(subscription.ToolId);
                            }
                        }
                        else
                        {
                            _subscriptionDemands.Remove(subscription.ToolId);
                        }

                        Error = subscription.Success ? null : subscription.Error;
                    }
                    else
                    {
                        Error = subscriptionError;
                    }

                    break;
                case RemoteMessageTypes.MiniToolActionResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolActionResponse action, out string actionError))
                    {
                        Error = action.Success ? null : action.Error;
                    }
                    else
                    {
                        Error = actionError;
                    }

                    break;
                case RemoteMessageTypes.MiniToolSample:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolSample sample, out _))
                        _samples[sample.ToolId] = sample;
                    break;
                case RemoteMessageTypes.MiniToolStreamBatch:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolStreamBatch batch, out _) && batch != null && !string.IsNullOrWhiteSpace(batch.ToolId))
                    {
                        if (!_streamBatches.TryGetValue(batch.ToolId, out Queue<RemoteMiniToolStreamBatch> batches))
                        {
                            batches = new Queue<RemoteMiniToolStreamBatch>();
                            _streamBatches.Add(batch.ToolId, batches);
                        }

                        while (batches.Count >= MaximumQueuedStreamBatchesPerTool)
                        {
                            batches.Dequeue();
                            batch.DroppedEventCount++;
                        }

                        batches.Enqueue(batch);
                    }

                    break;
            }

            _session.NotifyStateChanged();
        }

        public void Reset()
        {
            Tools = Array.Empty<RemoteMiniToolDescriptor>();
            _samples.Clear();
            _streamBatches.Clear();
            _subscriptions.Clear();
            _subscriptionDemands.Clear();
            Error = null;
        }
    }
}

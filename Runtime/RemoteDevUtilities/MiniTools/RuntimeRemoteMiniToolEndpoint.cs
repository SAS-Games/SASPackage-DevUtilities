using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools
{
    internal sealed class RuntimeRemoteMiniToolEndpoint : IRuntimeRemoteEndpoint, IRuntimeRemoteSessionListener
    {
        private sealed class ProviderState
        {
            public MiniToolProviderRegistration Registration;
            public bool Subscribed;
            public RemoteMiniToolDataChannels DataChannels;
            public float Interval;
            public double NextSampleTime;
            public float StreamInterval;
            public double NextStreamTime;
        }

        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.MiniToolCatalogRequest,
            RemoteMessageTypes.MiniToolSubscriptionRequest,
            RemoteMessageTypes.MiniToolActionRequest
        };

        private readonly Dictionary<string, ProviderState> _providers = new(StringComparer.OrdinalIgnoreCase);

        private RuntimeRemoteEndpointContext _context;

        public RuntimeRemoteMiniToolEndpoint()
        {
            foreach (MiniToolProviderRegistration registration in MiniToolRuntimeRegistry.CreateRegistrations())
            {
                RemoteMiniToolDescriptor descriptor = registration?.Descriptor;
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id) || _providers.ContainsKey(descriptor.Id))
                {
                    registration?.Dispose();
                    continue;
                }

                _providers.Add(descriptor.Id, new ProviderState
                {
                    Registration = registration,
                    Interval = Mathf.Max(0.1f, descriptor.DefaultIntervalSeconds),
                    StreamInterval = Mathf.Max(0.02f, descriptor.DefaultStreamIntervalSeconds)
                });
            }
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;

        public void Initialize(RuntimeRemoteEndpointContext context) => _context = context;

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.MiniToolCatalogRequest:
                    SendCatalog(envelope.RequestId);
                    break;
                case RemoteMessageTypes.MiniToolSubscriptionRequest:
                    SetSubscription(envelope);
                    break;
                case RemoteMessageTypes.MiniToolActionRequest:
                    ExecuteAction(envelope);
                    break;
            }
        }

        public void Tick()
        {
            if (_context == null || !_context.Settings.AllowMiniTools)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            foreach (ProviderState state in _providers.Values)
            {
                if (!state.Subscribed)
                    continue;

                state.Registration.Tick();
                if (RequiresSample(state.DataChannels) && now >= state.NextSampleTime)
                {
                    state.NextSampleTime = now + state.Interval;
                    RemoteMiniToolSample sample = state.Registration.Capture(state.DataChannels);
                    if (sample != null)
                        _context.Sender.Send(RemoteMessageTypes.MiniToolSample, 0, sample);
                }

                if ((state.DataChannels & RemoteMiniToolDataChannels.EventStream) == 0 || !state.Registration.SupportsEventStream || now < state.NextStreamTime)
                    continue;

                state.NextStreamTime = now + state.StreamInterval;
                RemoteMiniToolStreamBatch batch = state.Registration.CaptureStream();
                if (batch != null)
                    _context.Sender.Send(RemoteMessageTypes.MiniToolStreamBatch, 0, batch);
            }
        }

        public void Dispose()
        {
            foreach (ProviderState state in _providers.Values)
            {
                if (state.Subscribed)
                    state.Registration.Stop();
                state.Registration.Dispose();
            }

            _providers.Clear();
            _context = null;
        }

        public void OnRemoteSessionStateChanged(bool active)
        {
            if (active)
                return;

            foreach (ProviderState state in _providers.Values)
            {
                if (!state.Subscribed)
                    continue;
                state.Registration.Stop();
                state.Subscribed = false;
                state.DataChannels = RemoteMiniToolDataChannels.None;
                state.NextSampleTime = 0d;
                state.NextStreamTime = 0d;
            }
        }

        private void SendCatalog(long requestId)
        {
            var descriptors = new List<RemoteMiniToolDescriptor>(_providers.Count);
            foreach (ProviderState state in _providers.Values)
                descriptors.Add(state.Registration.Descriptor);

            _context.Sender.Send(RemoteMessageTypes.MiniToolCatalogResponse, requestId, new RemoteMiniToolCatalogResponse { Tools = descriptors.ToArray() });
        }

        private void SetSubscription(RemoteEnvelope envelope)
        {
            if (!_context.Settings.AllowMiniTools)
            {
                SendSubscriptionResult(envelope.RequestId, string.Empty, false, false, RemoteMiniToolDataChannels.None, "Remote mini-tools are disabled.");
                return;
            }

            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolSubscriptionRequest request, out string error))
            {
                SendSubscriptionResult(envelope.RequestId, string.Empty, false, false, RemoteMiniToolDataChannels.None, error);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.ToolId) || !_providers.TryGetValue(request.ToolId, out ProviderState state))
            {
                SendSubscriptionResult(envelope.RequestId, request.ToolId, false, false, RemoteMiniToolDataChannels.None, "The requested mini-tool is not available.");
                return;
            }

            if (request.Subscribe)
            {
                RemoteMiniToolDataChannels supportedChannels = GetSupportedDataChannels(state.Registration.Descriptor);
                RemoteMiniToolDataChannels unsupportedChannels = request.DataChannels & ~supportedChannels;
                if (unsupportedChannels != RemoteMiniToolDataChannels.None)
                {
                    SendSubscriptionResult(envelope.RequestId, request.ToolId, false, state.Subscribed, state.DataChannels, $"The requested data channels '{unsupportedChannels}' are not supported.");
                    return;
                }

                if (!state.Subscribed)
                {
                    state.Registration.Start();
                    state.Subscribed = true;
                }

                state.DataChannels = request.DataChannels;
                state.Interval = Mathf.Max(0.1f, request.IntervalSeconds > 0f ? request.IntervalSeconds : state.Registration.Descriptor.DefaultIntervalSeconds);
                state.StreamInterval = Mathf.Max(0.02f, request.StreamIntervalSeconds > 0f ? request.StreamIntervalSeconds : state.Registration.Descriptor.DefaultStreamIntervalSeconds);
                state.NextSampleTime = 0d;
                state.NextStreamTime = 0d;
            }
            else if (state.Subscribed)
            {
                state.Registration.Stop();
                state.Subscribed = false;
                state.DataChannels = RemoteMiniToolDataChannels.None;
            }

            SendSubscriptionResult(envelope.RequestId, request.ToolId, true, state.Subscribed, state.DataChannels, string.Empty);
        }

        private void SendSubscriptionResult(long requestId, string toolId, bool success, bool subscribed, RemoteMiniToolDataChannels dataChannels, string error)
        {
            _context.Sender.Send(RemoteMessageTypes.MiniToolSubscriptionResponse, requestId, new RemoteMiniToolSubscriptionResponse
            {
                ToolId = toolId,
                Success = success,
                Subscribed = subscribed,
                DataChannels = dataChannels,
                Error = error
            });
        }

        private static bool RequiresSample(RemoteMiniToolDataChannels dataChannels)
        {
            return (dataChannels & (RemoteMiniToolDataChannels.NativeWorkspaceFields | RemoteMiniToolDataChannels.TypedSnapshot)) != 0;
        }

        private static RemoteMiniToolDataChannels GetSupportedDataChannels(RemoteMiniToolDescriptor descriptor)
        {
            RemoteMiniToolDataChannels channels = RemoteMiniToolDataChannels.None;
            if ((descriptor.Capabilities & RemoteMiniToolCapabilities.NativeWorkspaceFields) != 0)
                channels |= RemoteMiniToolDataChannels.NativeWorkspaceFields;
            if ((descriptor.Capabilities & RemoteMiniToolCapabilities.TypedDebugHostSnapshot) != 0)
                channels |= RemoteMiniToolDataChannels.TypedSnapshot;
            if ((descriptor.Capabilities & RemoteMiniToolCapabilities.EventStream) != 0)
                channels |= RemoteMiniToolDataChannels.EventStream;
            return channels;
        }

        private void ExecuteAction(RemoteEnvelope envelope)
        {
            if (!_context.Settings.AllowMiniTools)
            {
                SendActionResult(envelope.RequestId, string.Empty, string.Empty, false, "Remote mini-tools are disabled.");
                return;
            }

            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolActionRequest request, out string error))
            {
                SendActionResult(envelope.RequestId, string.Empty, string.Empty, false, error);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.ToolId) || !_providers.TryGetValue(request.ToolId, out ProviderState state))
            {
                SendActionResult(envelope.RequestId, request.ToolId, request.ActionId, false, "The requested mini-tool is not available.");
                return;
            }

            if (!state.Subscribed)
            {
                SendActionResult(envelope.RequestId, request.ToolId, request.ActionId, false, "Start the mini-tool before using its actions.");
                return;
            }

            bool success = state.Registration.TryExecuteAction(request.ActionId, out error);
            if (success)
                state.NextSampleTime = 0d;

            SendActionResult(envelope.RequestId, request.ToolId, request.ActionId, success, error);
        }

        private void SendActionResult(long requestId, string toolId, string actionId, bool success, string error)
        {
            _context.Sender.Send(RemoteMessageTypes.MiniToolActionResponse, requestId, new RemoteMiniToolActionResponse
            {
                ToolId = toolId,
                ActionId = actionId,
                Success = success,
                Error = error
            });
        }
    }
}

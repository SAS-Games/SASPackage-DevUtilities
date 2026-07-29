using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Agent
{
    internal interface IRuntimeRemoteSender
    {
        bool RequiresAccessToken { get; }
        void Send<T>(string messageType, long requestId, T payload);
    }

    internal sealed class RuntimeRemoteEndpointContext
    {
        public IRuntimeRemoteSender Sender;
        public string RuntimeSessionId;
        public RemoteDevUtilitiesRuntimeSettings Settings;
    }

    internal interface IRuntimeRemoteEndpoint : IDisposable
    {
        IEnumerable<string> MessageTypes { get; }
        void Initialize(RuntimeRemoteEndpointContext context);
        void Handle(RemoteEnvelope envelope);
        void Tick();
    }

    internal interface IRuntimeRemoteSessionListener
    {
        void OnRemoteSessionStateChanged(bool active);
    }
}

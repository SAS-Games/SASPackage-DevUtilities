using System;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    internal interface IRuntimeRemoteTransport : IRuntimeRemoteSender, IDisposable
    {
        event Action<RemoteEnvelope> MessageReceived;
        event Action<int> EditorConnected;
        event Action<int> EditorDisconnected;

        void Start();
        void Tick();
    }
}

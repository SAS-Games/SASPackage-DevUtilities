using System;
using HP.Utilities.RemoteDevUtilities.Agent;
using HP.Utilities.RemoteDevUtilities.Protocol;

namespace HP.Utilities.RemoteDevUtilities.Transport
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

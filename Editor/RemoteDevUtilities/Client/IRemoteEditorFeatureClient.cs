using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Client
{
    internal interface IRemoteEditorSession
    {
        bool IsConnected { get; }
        long Send<T>(string messageType, T payload);
        void NotifyStateChanged();
    }

    internal interface IRemoteEditorFeatureClient
    {
        IEnumerable<string> MessageTypes { get; }
        void Handle(RemoteEnvelope envelope);
        void Reset();
    }
}

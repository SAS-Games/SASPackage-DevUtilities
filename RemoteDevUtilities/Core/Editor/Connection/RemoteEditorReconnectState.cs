using System;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [Serializable]
    internal sealed class RemoteEditorReconnectState
    {
        public RemoteEditorConnectionKind Kind;
        public string TransportId;
        public string TargetName;
        public int PlayerId = -1;
        public string Host;
        public int Port;
        public string AccessToken;

        public bool IsValid
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TransportId))
                {
                    if (string.Equals(TransportId, RemoteEditorTransportIds.LocalEditor, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(TransportId, RemoteEditorTransportIds.PlayerConnection, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (string.Equals(TransportId, RemoteEditorTransportIds.Tcp, StringComparison.OrdinalIgnoreCase))
                        return !string.IsNullOrWhiteSpace(Host) && Port >= 1 && Port <= 65535;
                }

                switch (Kind)
                {
                    case RemoteEditorConnectionKind.LocalEditor:
                        return true;
                    case RemoteEditorConnectionKind.PlayerConnection:
                        return true;
                    case RemoteEditorConnectionKind.DirectTcp:
                        return !string.IsNullOrWhiteSpace(Host) && Port >= 1 && Port <= 65535;
                    default:
                        return false;
                }
            }
        }
    }
}

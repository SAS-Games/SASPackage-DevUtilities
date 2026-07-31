using System;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [Serializable]
    internal sealed class RemoteEditorReconnectState
    {
        public RemoteEditorConnectionKind Kind;
        public int PlayerId = -1;
        public string Host;
        public int Port;
        public string AccessToken;

        public bool IsValid
        {
            get
            {
                switch (Kind)
                {
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

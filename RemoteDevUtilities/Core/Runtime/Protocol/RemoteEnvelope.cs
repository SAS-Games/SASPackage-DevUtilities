using System;

namespace HP.Utilities.RemoteDevUtilities.Protocol
{
    [Serializable]
    public sealed class RemoteEnvelope
    {
        public int ProtocolVersion;
        public string MessageType;
        public long RequestId;
        public string SessionId;
        public string PayloadJson;
    }
}

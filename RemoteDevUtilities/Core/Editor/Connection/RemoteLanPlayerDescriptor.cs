using HP.Utilities.RemoteDevUtilities.Protocol.Connection;

namespace HP.Utilities.RemoteDevUtilities.Editor.Connection
{
    internal sealed class RemoteLanPlayerDescriptor
    {
        public string RuntimeSessionId { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public int ProtocolVersion { get; set; }
        public string PackageVersion { get; set; }
        public RemoteTargetDescriptor Target { get; set; }
        public double LastSeenTime { get; set; }

        public bool IsProtocolCompatible => ProtocolVersion == Protocol.RemoteProtocolConstants.Version;
    }
}

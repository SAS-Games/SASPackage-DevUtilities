namespace SAS.Utilities.RemoteDevUtilities.Protocol
{
    public static class RemoteProtocolConstants
    {
        public const int Version = 1;
        public const string PackageVersion = "1.5.0";
        public const int MaximumMessageBytes = 8 * 1024 * 1024;
        public const int DefaultTcpPort = 56000;
    }

    public static class RemoteMessageTypes
    {
        public const string HandshakeRequest = "connection.handshake.request";
        public const string HandshakeResponse = "connection.handshake.response";
        public const string SessionEndRequest = "connection.session-end.request";
        public const string SessionEndResponse = "connection.session-end.response";
        public const string PingRequest = "connection.ping.request";
        public const string PingResponse = "connection.ping.response";

    }
}

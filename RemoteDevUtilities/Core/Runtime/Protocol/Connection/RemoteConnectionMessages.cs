using System;

namespace HP.Utilities.RemoteDevUtilities.Protocol.Connection
{
    [Serializable]
    public sealed class RemoteHandshakeRequest
    {
        public int ProtocolVersion;
        public string PackageVersion;
        public string EditorSessionId;
        public string AccessToken;
    }

    [Serializable]
    public sealed class RemoteHandshakeResponse
    {
        public bool Accepted;
        public string Error;
        public int ProtocolVersion;
        public string PackageVersion;
        public string RuntimeSessionId;
        public RemoteTargetDescriptor Target;
    }

    [Serializable]
    public sealed class RemoteTargetDescriptor
    {
        public string ProductName;
        public string ApplicationVersion;
        public string UnityVersion;
        public string Platform;
        public string DeviceName;
        public bool IsDebugBuild;
        public bool IsDevUtilitiesEnabled;
    }

    [Serializable]
    public sealed class RemotePingRequest
    {
        public double EditorTimestamp;
    }

    [Serializable]
    public sealed class RemoteSessionEndRequest
    {
        public string EditorSessionId;
    }

    [Serializable]
    public sealed class RemoteSessionEndResponse
    {
        public bool Ended;
    }

    [Serializable]
    public sealed class RemotePingResponse
    {
        public double EditorTimestamp;
        public double RuntimeTimestamp;
        public int RuntimeFrame;
    }
}

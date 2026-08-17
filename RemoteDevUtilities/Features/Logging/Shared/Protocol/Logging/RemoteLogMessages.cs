using System;

namespace HP.Utilities.RemoteDevUtilities.Protocol.Logging
{
    [Serializable]
    public sealed class RemoteLogSettingsRequest
    {
    }

    [Serializable]
    public sealed class RemoteLogSettingsResponse
    {
        public bool InfoEnabled;
        public bool WarningEnabled;
        public bool ErrorEnabled;
    }

    [Serializable]
    public sealed class RemoteLogBatch
    {
        public RemoteLogEntry[] Entries = Array.Empty<RemoteLogEntry>();
    }

    [Serializable]
    public sealed class RemoteLogEntry
    {
        public long Sequence;
        public double Timestamp;
        public int Frame;
        public int LogType;
        public string Message;
        public string StackTrace;
    }
}

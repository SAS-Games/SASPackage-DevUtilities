using System;

namespace SAS.Utilities.RemoteDevUtilities.Protocol.Logging
{
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

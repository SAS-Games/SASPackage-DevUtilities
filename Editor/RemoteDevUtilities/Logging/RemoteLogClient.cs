using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Logging;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Logging
{
    internal sealed class RemoteLogClient : IRemoteEditorFeatureClient
    {
        private const int MaximumEntries = 2000;
        private static readonly string[] SupportedMessages = { RemoteMessageTypes.LogBatch };
        private readonly IRemoteEditorSession _session;
        private readonly List<RemoteLogEntry> _entries = new();

        public RemoteLogClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public IReadOnlyList<RemoteLogEntry> Entries => _entries;

        public void Handle(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteLogBatch batch, out _))
                return;

            RemoteLogEntry[] entries = batch.Entries ?? Array.Empty<RemoteLogEntry>();
            _entries.AddRange(entries);
            if (_entries.Count > MaximumEntries)
                _entries.RemoveRange(0, _entries.Count - MaximumEntries);
            _session.NotifyStateChanged();
        }

        public void Clear()
        {
            _entries.Clear();
            _session.NotifyStateChanged();
        }

        public void Reset() => _entries.Clear();
    }
}

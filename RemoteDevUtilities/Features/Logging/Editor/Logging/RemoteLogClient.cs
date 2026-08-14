using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Logging;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Logging
{
    [RemoteEditorFeature("logging", 200)]
    internal sealed class RemoteLogClient : IRemoteEditorFeatureClient
    {
        private const int MaximumEntries = 2000;

        private static readonly string[] SupportedMessages =
        {
            RemoteLoggingMessageTypes.Batch,
            RemoteLoggingMessageTypes.SettingsResponse
        };

        private readonly IRemoteEditorSession _session;
        private readonly List<RemoteLogEntry> _entries = new();

        public RemoteLogClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public IReadOnlyList<RemoteLogEntry> Entries => _entries;
        public bool HasTargetSettings { get; private set; }
        public bool InfoEnabled { get; private set; }
        public bool WarningEnabled { get; private set; }
        public bool ErrorEnabled { get; private set; }

        public void RequestSettings()
        {
            _session.Send(RemoteLoggingMessageTypes.SettingsRequest, new RemoteLogSettingsRequest());
        }

        public void OnConnected() => RequestSettings();

        public void Handle(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteLoggingMessageTypes.SettingsResponse)
            {
                if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteLogSettingsResponse settings, out _))
                    return;

                HasTargetSettings = true;
                InfoEnabled = settings.InfoEnabled;
                WarningEnabled = settings.WarningEnabled;
                ErrorEnabled = settings.ErrorEnabled;
                _session.NotifyStateChanged();
                return;
            }

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

        public void Reset()
        {
            _entries.Clear();
            HasTargetSettings = false;
            InfoEnabled = false;
            WarningEnabled = false;
            ErrorEnabled = false;
        }
    }
}

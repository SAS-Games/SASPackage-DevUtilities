using System;
using System.Collections.Generic;
using System.Threading;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Logging;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace SAS.Utilities.RemoteDevUtilities.Logging
{
    [Preserve]
    [RuntimeRemoteEndpoint("logging", 200)]
    internal sealed class RuntimeRemoteLogEndpoint : IRuntimeRemoteEndpoint, IRuntimeRemoteSessionListener
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureRuntimeAssemblyIsLoaded()
        {
        }

        private static readonly string[] SupportedMessages =
        {
            RemoteLoggingMessageTypes.SettingsRequest
        };

        private readonly object _queueLock = new();
        private readonly Queue<RemoteLogEntry> _queue = new();
        private RuntimeRemoteEndpointContext _context;
        private long _sequence;
        private int _lastRuntimeFrame;
        private int _maxQueuedLogs;
        private bool _sessionActive;
        private int _settingsDirty;

        public IEnumerable<string> MessageTypes => SupportedMessages;

        public void Initialize(RuntimeRemoteEndpointContext context)
        {
            _context = context;
            _maxQueuedLogs = context.Settings.MaxQueuedLogs;
            Debug.LogLevelsChanged += OnLogLevelsChanged;
            if (context.Settings.StreamLogs)
                Application.logMessageReceivedThreaded += OnLog;
        }

        public void Handle(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteLoggingMessageTypes.SettingsRequest)
                SendSettings(envelope.RequestId);
        }

        public void Tick()
        {
            if (_context == null || !_sessionActive)
                return;

            if (Interlocked.Exchange(ref _settingsDirty, 0) != 0)
                SendSettings(0);

            if (!_context.Settings.StreamLogs)
                return;

            Volatile.Write(ref _lastRuntimeFrame, Time.frameCount);
            RemoteLogEntry[] entries;
            lock (_queueLock)
            {
                if (_queue.Count == 0)
                    return;

                int count = Math.Min(_queue.Count, _context.Settings.MaxLogsPerBatch);
                entries = new RemoteLogEntry[count];
                for (int i = 0; i < count; i++)
                    entries[i] = _queue.Dequeue();
            }

            _context.Sender.Send(RemoteLoggingMessageTypes.Batch, 0, new RemoteLogBatch { Entries = entries });
        }

        public void Dispose()
        {
            Debug.LogLevelsChanged -= OnLogLevelsChanged;
            Application.logMessageReceivedThreaded -= OnLog;
            lock (_queueLock)
                _queue.Clear();
            Volatile.Write(ref _settingsDirty, 0);
            _context = null;
        }

        public void OnRemoteSessionStateChanged(bool active)
        {
            _sessionActive = active;
            if (!active)
            {
                Volatile.Write(ref _settingsDirty, 0);
                lock (_queueLock)
                    _queue.Clear();
            }
        }

        private void OnLogLevelsChanged()
        {
            Volatile.Write(ref _settingsDirty, 1);
        }

        private void SendSettings(long requestId)
        {
            _context.Sender.Send(RemoteLoggingMessageTypes.SettingsResponse, requestId, new RemoteLogSettingsResponse
            {
                InfoEnabled = Debug.IsLogLevelEnabled(LogLevel.Info),
                WarningEnabled = Debug.IsLogLevelEnabled(LogLevel.Warning),
                ErrorEnabled = Debug.IsLogLevelEnabled(LogLevel.Error)
            });
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            var entry = new RemoteLogEntry
            {
                Sequence = Interlocked.Increment(ref _sequence),
                Timestamp = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond,
                Frame = Volatile.Read(ref _lastRuntimeFrame),
                LogType = (int)type,
                Message = condition ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty
            };

            lock (_queueLock)
            {
                while (_queue.Count >= _maxQueuedLogs)
                    _queue.Dequeue();
                _queue.Enqueue(entry);
            }
        }
    }
}

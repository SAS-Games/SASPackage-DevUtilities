using System;
using System.Collections.Generic;
using System.Threading;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Logging;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Logging
{
    internal sealed class RuntimeRemoteLogEndpoint :
        IRuntimeRemoteEndpoint,
        IRuntimeRemoteSessionListener
    {
        private static readonly string[] NoMessages = Array.Empty<string>();
        private readonly object _queueLock = new();
        private readonly Queue<RemoteLogEntry> _queue = new();
        private RuntimeRemoteEndpointContext _context;
        private long _sequence;
        private int _lastRuntimeFrame;
        private int _maxQueuedLogs;
        private bool _sessionActive;

        public IEnumerable<string> MessageTypes => NoMessages;

        public void Initialize(RuntimeRemoteEndpointContext context)
        {
            _context = context;
            _maxQueuedLogs = context.Settings.MaxQueuedLogs;
            if (context.Settings.StreamLogs)
                Application.logMessageReceivedThreaded += OnLog;
        }

        public void Handle(RemoteEnvelope envelope)
        {
        }

        public void Tick()
        {
            if (_context == null || !_context.Settings.StreamLogs || !_sessionActive)
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

            _context.Sender.Send(
                RemoteMessageTypes.LogBatch,
                0,
                new RemoteLogBatch { Entries = entries });
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            lock (_queueLock)
                _queue.Clear();
            _context = null;
        }

        public void OnRemoteSessionStateChanged(bool active)
        {
            _sessionActive = active;
            if (!active)
            {
                lock (_queueLock)
                    _queue.Clear();
            }
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

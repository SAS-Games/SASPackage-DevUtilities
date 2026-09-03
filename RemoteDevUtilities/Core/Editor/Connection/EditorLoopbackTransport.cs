using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RemoteDevUtilities.Transport;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteEditorTransportProvider(RemoteEditorTransportIds.LocalEditor, 50)]
    internal sealed class EditorLoopbackTransportProvider : IRemoteEditorTransportProvider
    {
        public IRemoteEditorTransport Create() => new EditorLoopbackTransport();
    }

    internal sealed class EditorLoopbackTransport : IRemoteEditorTransport
    {
        private readonly object _queueLock = new();
        private readonly Queue<RemoteEnvelope> _received = new();
        private bool _started;
        private bool _connectRequested;

        public string Id => RemoteEditorTransportIds.LocalEditor;
        public RemoteEditorConnectionKind Kind => RemoteEditorConnectionKind.LocalEditor;
        public bool IsReady => _connectRequested && IsRuntimeAvailable;
        internal bool IsRuntimeAvailable => EditorLoopbackChannel.HasRuntime;

        public event Action<RemoteEnvelope> MessageReceived;
        public event Action Ready;
        public event Action<string> Disconnected;

        public event Action<string> ConnectionFailed
        {
            add { }
            remove { }
        }

        public event Action TargetsChanged;

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            EditorLoopbackChannel.RuntimeAvailabilityChanged += OnRuntimeAvailabilityChanged;
            EditorLoopbackChannel.AttachEditor(this, Enqueue);
        }

        public void Tick()
        {
            while (true)
            {
                RemoteEnvelope envelope;
                lock (_queueLock)
                {
                    if (_received.Count == 0)
                        break;
                    envelope = _received.Dequeue();
                }

                MessageReceived?.Invoke(envelope);
            }
        }

        public void Connect(RemoteEditorTransportConnectRequest request)
        {
            Start();
            _connectRequested = true;
        }

        public void Disconnect()
        {
            _connectRequested = false;
            lock (_queueLock)
                _received.Clear();
        }

        public void Send<T>(string messageType, long requestId, string editorSessionId, T payload)
        {
            if (!IsReady)
                return;

            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, editorSessionId, payload);
            EditorLoopbackChannel.TrySendToRuntime(data);
        }

        public void Dispose()
        {
            if (_started)
            {
                EditorLoopbackChannel.RuntimeAvailabilityChanged -= OnRuntimeAvailabilityChanged;
                EditorLoopbackChannel.DetachEditor(this);
                _started = false;
            }

            Disconnect();
            MessageReceived = null;
            Ready = null;
            Disconnected = null;
            TargetsChanged = null;
        }

        private void Enqueue(byte[] data)
        {
            if (!RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out _))
                return;

            lock (_queueLock)
                _received.Enqueue(envelope);
        }

        private void OnRuntimeAvailabilityChanged()
        {
            TargetsChanged?.Invoke();
            if (!_connectRequested)
                return;

            if (IsRuntimeAvailable)
                Ready?.Invoke();
            else
                Disconnected?.Invoke("Editor Play Mode ended or its Remote Dev Utilities agent stopped.");
        }
    }
}

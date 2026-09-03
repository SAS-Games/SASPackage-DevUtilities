#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    /// <summary>
    /// In-process bridge used only while the game is running in the Editor.
    /// Messages remain serialized so local sessions exercise the same protocol
    /// boundary as Player Connection and TCP sessions.
    /// </summary>
    internal static class EditorLoopbackChannel
    {
        internal const int ConnectionId = 0;

        private static object s_EditorOwner;
        private static object s_RuntimeOwner;
        private static Action<byte[]> s_EditorReceiver;
        private static Action<byte[]> s_RuntimeReceiver;

        internal static event Action EditorAvailabilityChanged;
        internal static event Action RuntimeAvailabilityChanged;

        internal static bool HasEditor => s_EditorReceiver != null;
        internal static bool HasRuntime => s_RuntimeReceiver != null;

        internal static void AttachEditor(object owner, Action<byte[]> receiver)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (receiver == null)
                throw new ArgumentNullException(nameof(receiver));
            if (s_EditorOwner != null && !ReferenceEquals(s_EditorOwner, owner))
                throw new InvalidOperationException("Only one local Editor transport can be active.");

            bool changed = s_EditorReceiver == null;
            s_EditorOwner = owner;
            s_EditorReceiver = receiver;
            if (changed)
                EditorAvailabilityChanged?.Invoke();
        }

        internal static void DetachEditor(object owner)
        {
            if (!ReferenceEquals(s_EditorOwner, owner))
                return;

            bool changed = s_EditorReceiver != null;
            s_EditorOwner = null;
            s_EditorReceiver = null;
            if (changed)
                EditorAvailabilityChanged?.Invoke();
        }

        internal static void AttachRuntime(object owner, Action<byte[]> receiver)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (receiver == null)
                throw new ArgumentNullException(nameof(receiver));
            if (s_RuntimeOwner != null && !ReferenceEquals(s_RuntimeOwner, owner))
                throw new InvalidOperationException("Only one local runtime transport can be active.");

            bool changed = s_RuntimeReceiver == null;
            s_RuntimeOwner = owner;
            s_RuntimeReceiver = receiver;
            if (changed)
                RuntimeAvailabilityChanged?.Invoke();
        }

        internal static void DetachRuntime(object owner)
        {
            if (!ReferenceEquals(s_RuntimeOwner, owner))
                return;

            bool changed = s_RuntimeReceiver != null;
            s_RuntimeOwner = null;
            s_RuntimeReceiver = null;
            if (changed)
                RuntimeAvailabilityChanged?.Invoke();
        }

        internal static bool TrySendToEditor(byte[] data)
        {
            Action<byte[]> receiver = s_EditorReceiver;
            if (receiver == null)
                return false;
            receiver(data);
            return true;
        }

        internal static bool TrySendToRuntime(byte[] data)
        {
            Action<byte[]> receiver = s_RuntimeReceiver;
            if (receiver == null)
                return false;
            receiver(data);
            return true;
        }
    }

    [RuntimeRemoteTransportProvider("editor-loopback", 50)]
    internal sealed class RuntimeEditorLoopbackTransportProvider : IRuntimeRemoteTransportProvider
    {
        public IRuntimeRemoteTransport Create(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings) => new RuntimeEditorLoopbackTransport(runtimeSessionId);
    }

    internal sealed class RuntimeEditorLoopbackTransport : IRuntimeRemoteTransport
    {
        private readonly object _queueLock = new();
        private readonly Queue<RemoteEnvelope> _received = new();
        private readonly string _runtimeSessionId;
        private bool _started;

        internal RuntimeEditorLoopbackTransport(string runtimeSessionId)
        {
            _runtimeSessionId = runtimeSessionId;
        }

        public event Action<RemoteEnvelope> MessageReceived;
        public event Action<int> EditorConnected;
        public event Action<int> EditorDisconnected;

        public bool RequiresAccessToken => false;

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            EditorLoopbackChannel.EditorAvailabilityChanged += OnEditorAvailabilityChanged;
            EditorLoopbackChannel.AttachRuntime(this, Enqueue);
            if (EditorLoopbackChannel.HasEditor)
                EditorConnected?.Invoke(EditorLoopbackChannel.ConnectionId);
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

        public void Send<T>(string messageType, long requestId, T payload)
        {
            if (!_started)
                return;

            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, _runtimeSessionId, payload);
            EditorLoopbackChannel.TrySendToEditor(data);
        }

        public void Dispose()
        {
            if (_started)
            {
                EditorLoopbackChannel.EditorAvailabilityChanged -= OnEditorAvailabilityChanged;
                EditorLoopbackChannel.DetachRuntime(this);
                _started = false;
            }

            lock (_queueLock)
                _received.Clear();
            MessageReceived = null;
            EditorConnected = null;
            EditorDisconnected = null;
        }

        private void Enqueue(byte[] data)
        {
            if (!RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out _))
                return;

            lock (_queueLock)
                _received.Enqueue(envelope);
        }

        private void OnEditorAvailabilityChanged()
        {
            if (EditorLoopbackChannel.HasEditor)
                EditorConnected?.Invoke(EditorLoopbackChannel.ConnectionId);
            else
                EditorDisconnected?.Invoke(EditorLoopbackChannel.ConnectionId);
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    internal sealed class RuntimePlayerConnectionTransport : IRuntimeRemoteTransport
    {
        private sealed class CallbackHost : ScriptableObject
        {
            public Action<MessageEventArgs> MessageReceived;
            public Action<int> EditorConnected;
            public Action<int> EditorDisconnected;

            public void OnMessage(MessageEventArgs args) => MessageReceived?.Invoke(args);
            public void OnConnected(int playerId) => EditorConnected?.Invoke(playerId);
            public void OnDisconnected(int playerId) => EditorDisconnected?.Invoke(playerId);

            public void Clear()
            {
                MessageReceived = null;
                EditorConnected = null;
                EditorDisconnected = null;
            }
        }

        private readonly object _queueLock = new();
        private readonly Queue<RemoteEnvelope> _received = new();
        private readonly string _runtimeSessionId;
        private PlayerConnection _connection;
        private CallbackHost _callbackHost;

        public RuntimePlayerConnectionTransport(string runtimeSessionId)
        {
            _runtimeSessionId = runtimeSessionId;
        }

        public event Action<RemoteEnvelope> MessageReceived;
        public event Action<int> EditorConnected;
        public event Action<int> EditorDisconnected;
        public bool RequiresAccessToken => false;

        public void Start()
        {
            if (_connection != null)
                return;

            _connection = PlayerConnection.instance;
            _callbackHost = ScriptableObject.CreateInstance<CallbackHost>();
            _callbackHost.hideFlags = HideFlags.HideAndDontSave;
            _callbackHost.MessageReceived = OnMessage;
            _callbackHost.EditorConnected = OnConnected;
            _callbackHost.EditorDisconnected = OnDisconnected;
            _connection.Register(RemoteProtocolConstants.EditorToPlayerMessageId, _callbackHost.OnMessage);
            _connection.RegisterConnection(_callbackHost.OnConnected);
            _connection.RegisterDisconnection(_callbackHost.OnDisconnected);
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
            if (_connection == null || !_connection.isConnected)
                return;

            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, _runtimeSessionId, payload);
            _connection.Send(RemoteProtocolConstants.PlayerToEditorMessageId, data);
        }

        public void Dispose()
        {
            if (_connection == null)
                return;

            _connection.Unregister(RemoteProtocolConstants.EditorToPlayerMessageId, _callbackHost.OnMessage);
            _connection.UnregisterConnection(_callbackHost.OnConnected);
            _connection.UnregisterDisconnection(_callbackHost.OnDisconnected);
            _connection = null;

            _callbackHost.Clear();
            Object.Destroy(_callbackHost);
            _callbackHost = null;

            lock (_queueLock)
                _received.Clear();
        }

        private void OnMessage(MessageEventArgs args)
        {
            if (!RemoteProtocolSerializer.TryDeserializeEnvelope(args.data, out RemoteEnvelope envelope, out _))
                return;

            lock (_queueLock)
                _received.Enqueue(envelope);
        }

        private void OnConnected(int playerId) => EditorConnected?.Invoke(playerId);
        private void OnDisconnected(int playerId) => EditorDisconnected?.Invoke(playerId);
    }
}

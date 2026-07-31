using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    internal sealed class EditorPlayerConnectionTransport : IDisposable
    {
        private sealed class CallbackHost : ScriptableObject
        {
            public Action<MessageEventArgs> MessageReceived;
            public Action<int> PlayerConnected;
            public Action<int> PlayerDisconnected;

            public void OnMessage(MessageEventArgs args) => MessageReceived?.Invoke(args);
            public void OnConnected(int playerId) => PlayerConnected?.Invoke(playerId);
            public void OnDisconnected(int playerId) => PlayerDisconnected?.Invoke(playerId);

            public void Clear()
            {
                MessageReceived = null;
                PlayerConnected = null;
                PlayerDisconnected = null;
            }
        }

        private EditorConnection _connection;
        private CallbackHost _callbackHost;

        public event Action<int, RemoteEnvelope> MessageReceived;
        public event Action<int> PlayerConnected;
        public event Action<int> PlayerDisconnected;

        public void Start()
        {
            if (_connection != null)
                return;

            _connection = EditorConnection.instance;
            _connection.Initialize();
            _callbackHost = ScriptableObject.CreateInstance<CallbackHost>();
            _callbackHost.hideFlags = HideFlags.HideAndDontSave;
            _callbackHost.MessageReceived = OnMessage;
            _callbackHost.PlayerConnected = OnConnected;
            _callbackHost.PlayerDisconnected = OnDisconnected;
            _connection.Register(RemoteProtocolConstants.PlayerToEditorMessageId, _callbackHost.OnMessage);
            _connection.RegisterConnection(_callbackHost.OnConnected);
            _connection.RegisterDisconnection(_callbackHost.OnDisconnected);
        }

        public IReadOnlyList<RemoteEditorPlayerDescriptor> GetConnectedPlayers()
        {
            Start();
            var players = new List<RemoteEditorPlayerDescriptor>(_connection.ConnectedPlayers.Count);
            foreach (ConnectedPlayer player in _connection.ConnectedPlayers)
                players.Add(new RemoteEditorPlayerDescriptor(player.playerId, player.name));
            return players;
        }

        public void Send<T>(int playerId, string messageType, long requestId, string editorSessionId, T payload)
        {
            Start();
            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, editorSessionId, payload);
            _connection.Send(RemoteProtocolConstants.EditorToPlayerMessageId, data, playerId);
        }

        public void Dispose()
        {
            if (_connection == null)
                return;

            _connection.Unregister(RemoteProtocolConstants.PlayerToEditorMessageId, _callbackHost.OnMessage);
            _connection.UnregisterConnection(_callbackHost.OnConnected);
            _connection.UnregisterDisconnection(_callbackHost.OnDisconnected);
            _connection = null;

            _callbackHost.Clear();
            Object.DestroyImmediate(_callbackHost);
            _callbackHost = null;
        }

        private void OnMessage(MessageEventArgs args)
        {
            if (!RemoteProtocolSerializer.TryDeserializeEnvelope(args.data, out RemoteEnvelope envelope, out _))
                return;

            MessageReceived?.Invoke(args.playerId, envelope);
        }

        private void OnConnected(int playerId) => PlayerConnected?.Invoke(playerId);
        private void OnDisconnected(int playerId) => PlayerDisconnected?.Invoke(playerId);
    }
}

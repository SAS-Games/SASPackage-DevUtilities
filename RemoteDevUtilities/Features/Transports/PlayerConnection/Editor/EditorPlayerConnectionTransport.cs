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
    internal sealed class EditorPlayerConnectionTransport : IRemoteEditorTransport, IRemoteEditorPlayerTransport
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
        private int _selectedPlayerId = -1;
        private bool _hasSelectedPlayer;

        public string Id => RemoteEditorTransportIds.PlayerConnection;
        public RemoteEditorConnectionKind Kind => RemoteEditorConnectionKind.PlayerConnection;
        public bool IsReady => _hasSelectedPlayer;
        public event Action<RemoteEnvelope> MessageReceived;
        public event Action Ready { add { } remove { } }
        public event Action<string> Disconnected;
        public event Action<string> ConnectionFailed { add { } remove { } }
        public event Action TargetsChanged;

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
            _connection.Register(RemotePlayerConnectionProtocol.PlayerToEditorMessageId, _callbackHost.OnMessage);
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

        public void Tick()
        {
        }

        public void Connect(RemoteEditorTransportConnectRequest request)
        {
            Start();
            _selectedPlayerId = request?.PlayerId ?? -1;
            _hasSelectedPlayer = true;
        }

        public void Disconnect()
        {
            _selectedPlayerId = -1;
            _hasSelectedPlayer = false;
        }

        public void Send<T>(string messageType, long requestId, string editorSessionId, T payload)
        {
            if (!_hasSelectedPlayer)
                return;
            Start();
            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, editorSessionId, payload);
            _connection.Send(RemotePlayerConnectionProtocol.EditorToPlayerMessageId, data, _selectedPlayerId);
        }

        public void Dispose()
        {
            if (_connection == null)
                return;

            _connection.Unregister(RemotePlayerConnectionProtocol.PlayerToEditorMessageId, _callbackHost.OnMessage);
            _connection.UnregisterConnection(_callbackHost.OnConnected);
            _connection.UnregisterDisconnection(_callbackHost.OnDisconnected);
            _connection = null;
            _selectedPlayerId = -1;
            _hasSelectedPlayer = false;

            _callbackHost.Clear();
            Object.DestroyImmediate(_callbackHost);
            _callbackHost = null;
            MessageReceived = null;
            Disconnected = null;
            TargetsChanged = null;
        }

        private void OnMessage(MessageEventArgs args)
        {
            if (!RemoteProtocolSerializer.TryDeserializeEnvelope(args.data, out RemoteEnvelope envelope, out _))
                return;

            if (_hasSelectedPlayer && args.playerId == _selectedPlayerId)
                MessageReceived?.Invoke(envelope);
        }

        private void OnConnected(int playerId) => TargetsChanged?.Invoke();

        private void OnDisconnected(int playerId)
        {
            TargetsChanged?.Invoke();
            if (!_hasSelectedPlayer || playerId != _selectedPlayerId)
                return;

            _selectedPlayerId = -1;
            _hasSelectedPlayer = false;
            Disconnected?.Invoke("The Unity Player Connection target disconnected.");
        }
    }
}

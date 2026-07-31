using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection.Tcp;
using SAS.Utilities.RemoteDevUtilities.Editor.Logging;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEditor;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Client
{
    internal sealed class RemoteDevUtilitiesClient : IRemoteEditorSession, IDisposable
    {
        private const double HandshakeRetryIntervalSeconds = 0.75d;
        private const double HandshakeTimeoutSeconds = 8d;

        private readonly EditorPlayerConnectionTransport _playerTransport = new();
        private readonly EditorTcpConnectionTransport _tcpTransport = new();
        private readonly List<RemoteEditorPlayerDescriptor> _connectedPlayers = new();
        private readonly List<IRemoteEditorFeatureClient> _features = new();
        private readonly Dictionary<string, IRemoteEditorFeatureClient> _routes = new(StringComparer.Ordinal);
        private readonly string _editorSessionId = Guid.NewGuid().ToString("N");
        private long _nextRequestId;
        private double _nextHandshakeAttempt;
        private double _handshakeDeadline;
        private int _handshakeAttemptCount;
        private string _accessToken = string.Empty;
        private string _selectedTcpHost;
        private int _selectedTcpPort;

        public RemoteDevUtilitiesClient()
        {
            Commands = new RemoteCommandClient(this);
            Logs = new RemoteLogClient(this);
            MiniTools = new RemoteMiniToolClient(this);
            RuntimeSceneInspector = new RemoteRuntimeSceneInspectorClient(this);
            CommandPresentation = new RemoteCommandPresentationCoordinator(this);

            AddFeature(Commands);
            AddFeature(Logs);
            AddFeature(MiniTools);
            AddFeature(RuntimeSceneInspector);

            _playerTransport.MessageReceived += OnPlayerMessage;
            _playerTransport.PlayerConnected += OnPlayerListChanged;
            _playerTransport.PlayerDisconnected += OnPlayerDisconnected;
            _tcpTransport.Connected += OnTcpConnected;
            _tcpTransport.Disconnected += OnTcpDisconnected;
            _tcpTransport.ConnectionFailed += OnTcpConnectionFailed;
            _tcpTransport.MessageReceived += OnMessage;
            EditorApplication.update += Tick;
            _playerTransport.Start();
            RefreshConnectedPlayers();
        }

        public event Action StateChanged;

        public RemoteCommandClient Commands { get; }
        public RemoteCommandPresentationCoordinator CommandPresentation { get; }
        public RemoteLogClient Logs { get; }
        public RemoteMiniToolClient MiniTools { get; }
        public RemoteRuntimeSceneInspectorClient RuntimeSceneInspector { get; }
        public int SelectedPlayerId { get; private set; } = -1;
        public RemoteEditorConnectionKind ConnectionKind { get; private set; }
        public string SelectedTargetName { get; private set; }
        public bool HasSelectedTarget => ConnectionKind != RemoteEditorConnectionKind.None;
        public bool IsConnected { get; private set; }
        public bool IsHandshakePending { get; private set; }
        public string ConnectionError { get; private set; }
        public string RuntimeSessionId { get; private set; }
        public RemoteTargetDescriptor Target { get; private set; }
        public IReadOnlyList<RemoteEditorPlayerDescriptor> ConnectedPlayers => _connectedPlayers;

        public void RefreshConnectedPlayers()
        {
            _connectedPlayers.Clear();
            IReadOnlyList<RemoteEditorPlayerDescriptor> players = _playerTransport.GetConnectedPlayers();
            for (int i = 0; i < players.Count; i++)
                _connectedPlayers.Add(players[i]);
            NotifyStateChanged();
        }

        public void Connect(int playerId, string accessToken = null)
        {
            ResetSession();
            ConnectionKind = RemoteEditorConnectionKind.PlayerConnection;
            SelectedPlayerId = playerId;
            SelectedTargetName = $"Player {playerId}";
            _accessToken = accessToken ?? string.Empty;
            IsHandshakePending = true;
            double now = EditorApplication.timeSinceStartup;
            _handshakeDeadline = now + HandshakeTimeoutSeconds;
            _nextHandshakeAttempt = now;
            SendHandshake();
            NotifyStateChanged();
        }

        public void ConnectTcp(string host, int port, string accessToken = null)
        {
            ResetSession();
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
                return;

            string normalizedHost = host.Trim();
            ConnectionKind = RemoteEditorConnectionKind.DirectTcp;
            SelectedTargetName = $"{normalizedHost}:{port}";
            _selectedTcpHost = normalizedHost;
            _selectedTcpPort = port;
            _accessToken = accessToken ?? string.Empty;
            IsHandshakePending = true;
            double now = EditorApplication.timeSinceStartup;
            _handshakeDeadline = now + HandshakeTimeoutSeconds;
            _nextHandshakeAttempt = now;
            _tcpTransport.Connect(normalizedHost, port);
            NotifyStateChanged();
        }

        internal RemoteEditorReconnectState CaptureReconnectState()
        {
            if (!HasSelectedTarget)
                return null;

            return new RemoteEditorReconnectState
            {
                Kind = ConnectionKind,
                PlayerId = SelectedPlayerId,
                Host = _selectedTcpHost,
                Port = _selectedTcpPort,
                AccessToken = _accessToken
            };
        }

        public void CancelConnect()
        {
            if (!IsHandshakePending)
                return;

            ResetSession();
            NotifyStateChanged();
        }

        public void Disconnect()
        {
            if (HasSelectedTarget)
                Send(RemoteMessageTypes.SessionEndRequest, new RemoteSessionEndRequest { EditorSessionId = _editorSessionId });
            ResetSession();
            NotifyStateChanged();
        }

        public long Send<T>(string messageType, T payload)
        {
            if (!HasSelectedTarget)
                return 0;

            long requestId = ++_nextRequestId;
            if (ConnectionKind == RemoteEditorConnectionKind.PlayerConnection)
                _playerTransport.Send(SelectedPlayerId, messageType, requestId, _editorSessionId, payload);
            else if (ConnectionKind == RemoteEditorConnectionKind.DirectTcp)
                _tcpTransport.Send(messageType, requestId, _editorSessionId, payload);

            return requestId;
        }

        public void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        public void Dispose()
        {
            EditorApplication.update -= Tick;
            Disconnect();
            _playerTransport.MessageReceived -= OnPlayerMessage;
            _playerTransport.PlayerConnected -= OnPlayerListChanged;
            _playerTransport.PlayerDisconnected -= OnPlayerDisconnected;
            _tcpTransport.Connected -= OnTcpConnected;
            _tcpTransport.Disconnected -= OnTcpDisconnected;
            _tcpTransport.ConnectionFailed -= OnTcpConnectionFailed;
            _tcpTransport.MessageReceived -= OnMessage;
            _playerTransport.Dispose();
            _tcpTransport.Dispose();
            _connectedPlayers.Clear();
            _features.Clear();
            _routes.Clear();
            StateChanged = null;
        }

        private void Tick()
        {
            _tcpTransport.Tick();

            if (!IsHandshakePending || !HasSelectedTarget)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now >= _handshakeDeadline)
            {
                string target = SelectedTargetName ?? "the Player";
                FailConnection($"No handshake response from {target} after {_handshakeAttemptCount} " + "attempts. The Player may still be starting; press Connect to retry.");
                return;
            }

            if (now >= _nextHandshakeAttempt && (ConnectionKind != RemoteEditorConnectionKind.DirectTcp || _tcpTransport.IsConnected))
                SendHandshake();
        }

        private void SendHandshake()
        {
            _handshakeAttemptCount++;
            _nextHandshakeAttempt = EditorApplication.timeSinceStartup + HandshakeRetryIntervalSeconds;
            Send(RemoteMessageTypes.HandshakeRequest, new RemoteHandshakeRequest
            {
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                EditorSessionId = _editorSessionId,
                AccessToken = _accessToken
            });
        }

        private void AddFeature(IRemoteEditorFeatureClient feature)
        {
            _features.Add(feature);
            foreach (string messageType in feature.MessageTypes)
                _routes[messageType] = feature;
        }

        private void OnPlayerMessage(int playerId, RemoteEnvelope envelope)
        {
            if (ConnectionKind != RemoteEditorConnectionKind.PlayerConnection || playerId != SelectedPlayerId)
                return;

            OnMessage(envelope);
        }

        private void OnMessage(RemoteEnvelope envelope)
        {
            if (envelope == null)
                return;

            if (envelope.MessageType == RemoteMessageTypes.HandshakeResponse)
            {
                HandleHandshake(envelope);
                return;
            }

            if (!IsConnected || envelope.ProtocolVersion != RemoteProtocolConstants.Version || (!string.IsNullOrEmpty(RuntimeSessionId) && !string.Equals(envelope.SessionId, RuntimeSessionId, StringComparison.Ordinal)))
                return;

            if (_routes.TryGetValue(envelope.MessageType, out IRemoteEditorFeatureClient feature))
                feature.Handle(envelope);
        }

        private void HandleHandshake(RemoteEnvelope envelope)
        {
            IsHandshakePending = false;
            _nextHandshakeAttempt = 0d;
            _handshakeDeadline = 0d;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteHandshakeResponse response, out string error))
            {
                FailConnection(error);
                return;
            }

            if (!response.Accepted)
            {
                FailConnection(response.Error ?? "The runtime rejected the connection.");
                return;
            }

            if (envelope.ProtocolVersion != RemoteProtocolConstants.Version || response.ProtocolVersion != RemoteProtocolConstants.Version)
            {
                FailConnection($"Protocol mismatch. Editor={RemoteProtocolConstants.Version}, Runtime={response.ProtocolVersion}.");
                return;
            }

            if (string.IsNullOrWhiteSpace(response.RuntimeSessionId))
            {
                FailConnection("The runtime handshake did not include a session identifier.");
                return;
            }

            IsConnected = true;
            ConnectionError = null;
            RuntimeSessionId = response.RuntimeSessionId;
            Target = response.Target;
            Commands.RequestCatalog();
            Logs.RequestSettings();
            MiniTools.RequestCatalog();
            RuntimeSceneInspector.RequestHierarchy(true);
            NotifyStateChanged();
        }

        private void OnPlayerListChanged(int playerId) => RefreshConnectedPlayers();

        private void OnPlayerDisconnected(int playerId)
        {
            if (ConnectionKind == RemoteEditorConnectionKind.PlayerConnection && playerId == SelectedPlayerId)
                ResetSession();
            RefreshConnectedPlayers();
        }

        private void OnTcpConnected()
        {
            if (ConnectionKind != RemoteEditorConnectionKind.DirectTcp || !IsHandshakePending)
                return;

            _nextHandshakeAttempt = EditorApplication.timeSinceStartup;
            SendHandshake();
            NotifyStateChanged();
        }

        private void OnTcpDisconnected(string error)
        {
            if (ConnectionKind != RemoteEditorConnectionKind.DirectTcp)
                return;

            FailConnection(string.IsNullOrWhiteSpace(error) ? "The TCP connection to the Player closed." : error);
        }

        private void OnTcpConnectionFailed(string error)
        {
            if (ConnectionKind != RemoteEditorConnectionKind.DirectTcp)
                return;

            FailConnection("Could not connect to the Player by direct IP: " + (string.IsNullOrWhiteSpace(error) ? "Unknown TCP error." : error));
        }

        private void ResetSession()
        {
            bool disconnectTcp = ConnectionKind == RemoteEditorConnectionKind.DirectTcp;
            IsConnected = false;
            IsHandshakePending = false;
            _nextHandshakeAttempt = 0d;
            _handshakeDeadline = 0d;
            _handshakeAttemptCount = 0;
            ConnectionError = null;
            RuntimeSessionId = null;
            Target = null;
            ConnectionKind = RemoteEditorConnectionKind.None;
            SelectedTargetName = null;
            SelectedPlayerId = -1;
            _selectedTcpHost = null;
            _selectedTcpPort = 0;
            _accessToken = string.Empty;
            for (int i = 0; i < _features.Count; i++)
                _features[i].Reset();
            if (disconnectTcp)
                _tcpTransport.Disconnect();
        }

        private void FailConnection(string error)
        {
            ResetSession();
            ConnectionError = string.IsNullOrWhiteSpace(error) ? "The remote connection failed." : error;
            NotifyStateChanged();
        }
    }
}

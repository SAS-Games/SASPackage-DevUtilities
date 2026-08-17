using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
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

        private readonly List<IRemoteEditorTransport> _transports = new();
        private readonly Dictionary<string, IRemoteEditorTransport> _transportsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IRemoteEditorConnectionService> _connectionServices = new();
        private readonly List<RemoteEditorPlayerDescriptor> _connectedPlayers = new();
        private readonly List<IRemoteEditorFeatureClient> _features = new();
        private readonly Dictionary<string, IRemoteEditorFeatureClient> _routes = new(StringComparer.Ordinal);
        private readonly string _editorSessionId = Guid.NewGuid().ToString("N");
        private IRemoteEditorTransport _activeTransport;
        private RemoteEditorTransportConnectRequest _activeRequest;
        private long _nextRequestId;
        private double _nextHandshakeAttempt;
        private double _handshakeDeadline;
        private int _handshakeAttemptCount;
        private string _accessToken = string.Empty;

        public RemoteDevUtilitiesClient()
        {
            foreach (IRemoteEditorFeatureClient feature in RemoteEditorFeatureRegistry.CreateFeatures(this))
                AddFeature(feature);

            foreach (IRemoteEditorTransport transport in RemoteEditorTransportRegistry.CreateTransports())
                AddTransport(transport);
            foreach (IRemoteEditorConnectionService service in RemoteEditorConnectionServiceRegistry.CreateServices())
            {
                service.Start(this);
                _connectionServices.Add(service);
            }

            EditorApplication.update += Tick;
            RefreshConnectedPlayers();
        }

        public event Action StateChanged;

        public int SelectedPlayerId { get; private set; } = -1;
        public RemoteEditorConnectionKind ConnectionKind { get; private set; }
        public string SelectedTransportId => _activeTransport?.Id;
        public string SelectedTargetName { get; private set; }
        public bool HasSelectedTarget => _activeTransport != null;
        public bool IsConnected { get; private set; }
        public bool IsHandshakePending { get; private set; }
        public string ConnectionError { get; private set; }
        public string RuntimeSessionId { get; private set; }
        public RemoteTargetDescriptor Target { get; private set; }
        public IReadOnlyList<RemoteEditorPlayerDescriptor> ConnectedPlayers => _connectedPlayers;
        public IReadOnlyList<RemoteLanPlayerDescriptor> LanPlayers => FindConnectionService<IRemoteLanDiscoveryService>()?.Players ?? Array.Empty<RemoteLanPlayerDescriptor>();
        public string LanDiscoveryError => FindConnectionService<IRemoteLanDiscoveryService>()?.Error;
        internal bool HasTransport(string transportId) => !string.IsNullOrWhiteSpace(transportId) && _transportsById.ContainsKey(transportId);
        internal bool HasConnectionService<T>() where T : class, IRemoteEditorConnectionService => FindConnectionService<T>() != null;

        internal bool TryGetTransport<T>(out T transport) where T : class, IRemoteEditorTransport
        {
            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i] is T candidate)
                {
                    transport = candidate;
                    return true;
                }
            }

            transport = null;
            return false;
        }

        internal bool TryGetFeature<T>(out T feature) where T : class, IRemoteEditorFeatureClient
        {
            for (int i = 0; i < _features.Count; i++)
            {
                if (_features[i] is T candidate)
                {
                    feature = candidate;
                    return true;
                }
            }

            feature = null;
            return false;
        }

        internal T GetRequiredFeature<T>() where T : class, IRemoteEditorFeatureClient
        {
            if (TryGetFeature(out T feature))
                return feature;
            throw new InvalidOperationException($"Required remote editor feature '{typeof(T).FullName}' is not registered.");
        }

        public void RefreshConnectedPlayers()
        {
            _connectedPlayers.Clear();
            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i] is not IRemoteEditorPlayerTransport playerTransport)
                    continue;
                IReadOnlyList<RemoteEditorPlayerDescriptor> players = playerTransport.GetConnectedPlayers();
                for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
                    _connectedPlayers.Add(players[playerIndex]);
            }
            NotifyStateChanged();
        }

        public void RefreshLanPlayers()
        {
            if (FindConnectionService<IRemoteLanDiscoveryService>()?.Clear() == true)
                NotifyStateChanged();
        }

        public void Connect(int playerId, string accessToken = null)
        {
            ConnectTransport(RemoteEditorTransportIds.PlayerConnection, new RemoteEditorTransportConnectRequest
            {
                PlayerId = playerId,
                TargetName = $"Player {playerId}",
                AccessToken = accessToken
            });
        }

        public void ConnectTcp(string host, int port, string accessToken = null)
        {
            if (string.IsNullOrWhiteSpace(host) || port < 1 || port > 65535)
                return;
            string normalizedHost = host.Trim();
            ConnectTransport(RemoteEditorTransportIds.Tcp, new RemoteEditorTransportConnectRequest
            {
                Host = normalizedHost,
                Port = port,
                TargetName = $"{normalizedHost}:{port}",
                AccessToken = accessToken
            });
        }

        internal bool ConnectTransport(string transportId, RemoteEditorTransportConnectRequest request)
        {
            ResetSession();
            if (!_transportsById.TryGetValue(transportId ?? string.Empty, out IRemoteEditorTransport transport))
            {
                ConnectionError = $"The '{transportId}' remote transport is not installed.";
                NotifyStateChanged();
                return false;
            }

            _activeTransport = transport;
            _activeRequest = request ?? new RemoteEditorTransportConnectRequest();
            ConnectionKind = transport.Kind;
            SelectedPlayerId = _activeRequest.PlayerId;
            SelectedTargetName = string.IsNullOrWhiteSpace(_activeRequest.TargetName) ? transport.Id : _activeRequest.TargetName;
            _accessToken = _activeRequest.AccessToken ?? string.Empty;
            IsHandshakePending = true;
            double now = EditorApplication.timeSinceStartup;
            _handshakeDeadline = now + HandshakeTimeoutSeconds;
            _nextHandshakeAttempt = now;
            transport.Connect(_activeRequest);
            if (transport.IsReady)
                SendHandshake();
            NotifyStateChanged();
            return true;
        }

        internal RemoteEditorReconnectState CaptureReconnectState()
        {
            if (!HasSelectedTarget)
                return null;

            return new RemoteEditorReconnectState
            {
                TransportId = _activeTransport.Id,
                Kind = ConnectionKind,
                PlayerId = _activeRequest?.PlayerId ?? -1,
                Host = _activeRequest?.Host,
                Port = _activeRequest?.Port ?? 0,
                AccessToken = _accessToken,
                TargetName = SelectedTargetName
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
            if (_activeTransport == null)
                return 0;
            long requestId = ++_nextRequestId;
            _activeTransport.Send(messageType, requestId, _editorSessionId, payload);
            return requestId;
        }

        public void NotifyStateChanged() => StateChanged?.Invoke();

        public void Dispose()
        {
            EditorApplication.update -= Tick;
            Disconnect();
            for (int i = _connectionServices.Count - 1; i >= 0; i--)
                _connectionServices[i].Dispose();
            _connectionServices.Clear();
            for (int i = _transports.Count - 1; i >= 0; i--)
                _transports[i].Dispose();
            _transports.Clear();
            _transportsById.Clear();
            _connectedPlayers.Clear();
            _features.Clear();
            _routes.Clear();
            StateChanged = null;
        }

        private void AddTransport(IRemoteEditorTransport transport)
        {
            if (transport == null || string.IsNullOrWhiteSpace(transport.Id))
                return;
            if (_transportsById.ContainsKey(transport.Id))
                throw new InvalidOperationException($"Duplicate editor remote transport id '{transport.Id}'.");

            _transports.Add(transport);
            _transportsById.Add(transport.Id, transport);
            transport.MessageReceived += envelope => OnTransportMessage(transport, envelope);
            transport.Ready += () => OnTransportReady(transport);
            transport.Disconnected += error => OnTransportDisconnected(transport, error);
            transport.ConnectionFailed += error => OnTransportConnectionFailed(transport, error);
            transport.TargetsChanged += RefreshConnectedPlayers;
            transport.Start();
        }

        private void Tick()
        {
            for (int i = 0; i < _transports.Count; i++)
                _transports[i].Tick();
            bool serviceChanged = false;
            for (int i = 0; i < _connectionServices.Count; i++)
                serviceChanged |= _connectionServices[i].Tick(EditorApplication.timeSinceStartup);
            if (serviceChanged)
                NotifyStateChanged();

            if (!IsHandshakePending || _activeTransport == null)
                return;
            double now = EditorApplication.timeSinceStartup;
            if (now >= _handshakeDeadline)
            {
                string target = SelectedTargetName ?? "the Player";
                FailConnection($"No handshake response from {target} after {_handshakeAttemptCount} attempts. The Player may still be starting; press Connect to retry.");
                return;
            }

            if (now >= _nextHandshakeAttempt && _activeTransport.IsReady)
                SendHandshake();
        }

        private void SendHandshake()
        {
            if (!IsHandshakePending || _activeTransport == null || !_activeTransport.IsReady)
                return;
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

        private T FindConnectionService<T>() where T : class, IRemoteEditorConnectionService
        {
            for (int i = 0; i < _connectionServices.Count; i++)
            {
                if (_connectionServices[i] is T match)
                    return match;
            }
            return null;
        }

        private void OnTransportMessage(IRemoteEditorTransport transport, RemoteEnvelope envelope)
        {
            if (!ReferenceEquals(transport, _activeTransport))
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
            if (!IsConnected || envelope.ProtocolVersion != RemoteProtocolConstants.Version ||
                (!string.IsNullOrEmpty(RuntimeSessionId) && !string.Equals(envelope.SessionId, RuntimeSessionId, StringComparison.Ordinal)))
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
            for (int i = 0; i < _features.Count; i++)
                _features[i].OnConnected();
            NotifyStateChanged();
        }

        private void OnTransportReady(IRemoteEditorTransport transport)
        {
            if (!ReferenceEquals(transport, _activeTransport) || !IsHandshakePending)
                return;
            _nextHandshakeAttempt = EditorApplication.timeSinceStartup;
            SendHandshake();
            NotifyStateChanged();
        }

        private void OnTransportDisconnected(IRemoteEditorTransport transport, string error)
        {
            if (ReferenceEquals(transport, _activeTransport))
                FailConnection(string.IsNullOrWhiteSpace(error) ? "The connection to the Player closed." : error);
        }

        private void OnTransportConnectionFailed(IRemoteEditorTransport transport, string error)
        {
            if (ReferenceEquals(transport, _activeTransport))
                FailConnection(string.IsNullOrWhiteSpace(error) ? "Could not connect to the Player." : error);
        }

        private void ResetSession()
        {
            IRemoteEditorTransport previousTransport = _activeTransport;
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
            _accessToken = string.Empty;
            _activeTransport = null;
            _activeRequest = null;
            for (int i = 0; i < _features.Count; i++)
                _features[i].Reset();
            previousTransport?.Disconnect();
        }

        private void FailConnection(string error)
        {
            ResetSession();
            ConnectionError = string.IsNullOrWhiteSpace(error) ? "The remote connection failed." : error;
            NotifyStateChanged();
        }
    }
}

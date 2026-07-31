using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Commands;
using SAS.Utilities.RemoteDevUtilities.Logging;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Presentation;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Transport;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Agent
{
    [RuntimeSceneInspectorProtected]
    public sealed class RuntimeDevUtilitiesAgent : MonoBehaviour
    {
        private readonly List<IRuntimeRemoteEndpoint> _endpoints = new();
        private readonly Dictionary<string, IRuntimeRemoteEndpoint> _routes = new(StringComparer.Ordinal);

        private RemoteDevUtilitiesRuntimeSettings _settings;
        private IRuntimeRemoteTransport _transport;
        private RuntimeConnectionEndpoint _connectionEndpoint;
        private RuntimeBackgroundExecutionLease _backgroundExecution;
        private string _runtimeSessionId;

        public static RuntimeDevUtilitiesAgent Instance { get; private set; }
        public bool IsInitialized => _transport != null;

        internal void Initialize(RemoteDevUtilitiesRuntimeSettings settings)
        {
            _settings = settings;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _settings ??= RemoteDevUtilitiesRuntimeSettings.LoadOrCreateDefaults();
            ApplyPresentationPolicy(_settings, false);
            StartSubsystem();
        }

        private void Update()
        {
            _transport?.Tick();
            for (int i = 0; i < _endpoints.Count; i++)
                _endpoints[i].Tick();
        }

        private void OnDestroy()
        {
            StopSubsystem();
            if (Instance == this)
                Instance = null;
        }

        private void StartSubsystem()
        {
            if (_transport != null || !_settings.EnableRemoteAgent)
                return;

            _backgroundExecution = new RuntimeBackgroundExecutionLease();
            _backgroundExecution.Acquire(_settings.KeepPlayerRunningInBackground);
            _runtimeSessionId = Guid.NewGuid().ToString("N");
            _transport = RuntimeRemoteTransportFactory.Create(_runtimeSessionId, _settings);
            _transport.MessageReceived += OnMessage;
            _transport.EditorDisconnected += OnEditorDisconnected;

            var context = new RuntimeRemoteEndpointContext
            {
                Sender = _transport,
                RuntimeSessionId = _runtimeSessionId,
                Settings = _settings
            };

            _connectionEndpoint = new RuntimeConnectionEndpoint();
            _connectionEndpoint.SessionStateChanged += OnSessionStateChanged;

            AddEndpoint(_connectionEndpoint, context);
            AddEndpoint(new RuntimeRemoteCommandEndpoint(), context);
            AddEndpoint(new RuntimeRemoteLogEndpoint(), context);
            AddEndpoint(new RuntimeRemoteMiniToolEndpoint(), context);
            AddEndpoint(new RemoteRuntimeSceneInspectorEndpoint(), context);
            _transport.Start();
        }

        private void StopSubsystem()
        {
            ApplyPresentationPolicy(_settings, false);

            if (_connectionEndpoint != null)
                _connectionEndpoint.SessionStateChanged -= OnSessionStateChanged;
            _connectionEndpoint = null;

            for (int i = _endpoints.Count - 1; i >= 0; i--)
                _endpoints[i].Dispose();
            _endpoints.Clear();
            _routes.Clear();

            if (_transport != null)
            {
                _transport.MessageReceived -= OnMessage;
                _transport.EditorDisconnected -= OnEditorDisconnected;
                _transport.Dispose();
                _transport = null;
            }

            _backgroundExecution?.Dispose();
            _backgroundExecution = null;
        }

        private void AddEndpoint(IRuntimeRemoteEndpoint endpoint, RuntimeRemoteEndpointContext context)
        {
            endpoint.Initialize(context);
            _endpoints.Add(endpoint);
            foreach (string messageType in endpoint.MessageTypes)
            {
                if (!string.IsNullOrWhiteSpace(messageType))
                    _routes[messageType] = endpoint;
            }
        }

        private void OnMessage(RemoteEnvelope envelope)
        {
            if (envelope == null)
                return;

            bool isHandshake = envelope.MessageType == RemoteMessageTypes.HandshakeRequest;
            if (!isHandshake && envelope.ProtocolVersion != RemoteProtocolConstants.Version)
                return;
            if (!isHandshake && (!_connectionEndpoint.IsSessionAccepted || !string.Equals(envelope.SessionId, _connectionEndpoint.AcceptedEditorSessionId, StringComparison.Ordinal)))
                return;

            if (_routes.TryGetValue(envelope.MessageType, out IRuntimeRemoteEndpoint endpoint))
                endpoint.Handle(envelope);
        }

        private void OnSessionStateChanged(bool active)
        {
            ApplyPresentationPolicy(_settings, active);
            for (int i = 0; i < _endpoints.Count; i++)
            {
                if (_endpoints[i] is IRuntimeRemoteSessionListener listener)
                    listener.OnRemoteSessionStateChanged(active);
            }
        }

        private void OnEditorDisconnected(int playerId)
        {
            if (_connectionEndpoint != null)
                _connectionEndpoint.NotifyDisconnected();
            else
                ApplyPresentationPolicy(_settings, false);
        }

        internal static void ApplyPresentationPolicy(RemoteDevUtilitiesRuntimeSettings settings, bool remoteSessionActive)
        {
            BuildDebugUiVisibility visibility = settings != null ? settings.BuildUiVisibility : BuildDebugUiVisibility.ShowWhenEnabled;
            RemoteDevUtilitiesPresentation.Configure(visibility);
            RemoteDevUtilitiesPresentation.SetRemoteSessionActive(remoteSessionActive);
        }
    }
}

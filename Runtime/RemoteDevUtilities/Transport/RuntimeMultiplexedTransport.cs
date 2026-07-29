using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    internal sealed class RuntimeMultiplexedTransport : IRuntimeRemoteTransport
    {
        private sealed class Registration
        {
            private readonly RuntimeMultiplexedTransport _owner;

            public Registration(
                RuntimeMultiplexedTransport owner,
                IRuntimeRemoteTransport transport,
                int connectionId)
            {
                _owner = owner;
                Transport = transport;
                ConnectionId = connectionId;
            }

            public IRuntimeRemoteTransport Transport { get; }
            public int ConnectionId { get; }

            public void Subscribe()
            {
                Transport.MessageReceived += OnMessageReceived;
                Transport.EditorConnected += OnEditorConnected;
                Transport.EditorDisconnected += OnEditorDisconnected;
            }

            public void Unsubscribe()
            {
                Transport.MessageReceived -= OnMessageReceived;
                Transport.EditorConnected -= OnEditorConnected;
                Transport.EditorDisconnected -= OnEditorDisconnected;
            }

            private void OnMessageReceived(RemoteEnvelope envelope) =>
                _owner.OnMessageReceived(this, envelope);

            private void OnEditorConnected(int _) =>
                _owner.OnEditorConnected(ConnectionId);

            private void OnEditorDisconnected(int _) =>
                _owner.OnEditorDisconnected(this);
        }

        private readonly List<Registration> _registrations = new();
        private IRuntimeRemoteTransport _activeTransport;
        private IRuntimeRemoteTransport _replyTransport;
        private bool _started;

        public RuntimeMultiplexedTransport(
            params IRuntimeRemoteTransport[] transports)
        {
            if (transports == null || transports.Length == 0)
                throw new ArgumentException(
                    "At least one runtime transport is required.",
                    nameof(transports));

            for (int i = 0; i < transports.Length; i++)
            {
                IRuntimeRemoteTransport transport = transports[i] ??
                    throw new ArgumentException(
                        "Runtime transports cannot contain null values.",
                        nameof(transports));
                _registrations.Add(new Registration(this, transport, i));
            }
        }

        public event Action<RemoteEnvelope> MessageReceived;
        public event Action<int> EditorConnected;
        public event Action<int> EditorDisconnected;

        public bool RequiresAccessToken =>
            (_replyTransport ?? _activeTransport)?.RequiresAccessToken ??
            false;

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            for (int i = 0; i < _registrations.Count; i++)
            {
                Registration registration = _registrations[i];
                registration.Subscribe();
                registration.Transport.Start();
            }
        }

        public void Tick()
        {
            for (int i = 0; i < _registrations.Count; i++)
                _registrations[i].Transport.Tick();
        }

        public void Send<T>(string messageType, long requestId, T payload)
        {
            IRuntimeRemoteTransport destination =
                _replyTransport ?? _activeTransport;
            if (destination == null)
                return;

            if (messageType == RemoteMessageTypes.HandshakeResponse &&
                payload is RemoteHandshakeResponse response &&
                response.Accepted)
            {
                _activeTransport = destination;
            }

            destination.Send(messageType, requestId, payload);
        }

        public void Dispose()
        {
            for (int i = _registrations.Count - 1; i >= 0; i--)
            {
                Registration registration = _registrations[i];
                registration.Unsubscribe();
                registration.Transport.Dispose();
            }

            _registrations.Clear();
            _activeTransport = null;
            _replyTransport = null;
            _started = false;
            MessageReceived = null;
            EditorConnected = null;
            EditorDisconnected = null;
        }

        private void OnMessageReceived(
            Registration registration,
            RemoteEnvelope envelope)
        {
            if (envelope == null)
                return;

            bool isHandshake =
                envelope.MessageType == RemoteMessageTypes.HandshakeRequest;
            if (!isHandshake &&
                !ReferenceEquals(
                    _activeTransport,
                    registration.Transport))
            {
                return;
            }

            _replyTransport = registration.Transport;
            try
            {
                MessageReceived?.Invoke(envelope);
            }
            finally
            {
                _replyTransport = null;
            }
        }

        private void OnEditorConnected(int connectionId) =>
            EditorConnected?.Invoke(connectionId);

        private void OnEditorDisconnected(Registration registration)
        {
            if (!ReferenceEquals(_activeTransport, registration.Transport))
                return;

            _activeTransport = null;
            EditorDisconnected?.Invoke(registration.ConnectionId);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Transport.Tcp
{
    internal sealed class RuntimeTcpServerTransport : IRuntimeRemoteTransport, IRuntimeTcpEndpoint
    {
        private enum NotificationType
        {
            Connected,
            Disconnected,
            Message
        }

        private readonly struct Notification
        {
            public Notification(NotificationType type, RemoteEnvelope envelope = null)
            {
                Type = type;
                Envelope = envelope;
            }

            public NotificationType Type { get; }
            public RemoteEnvelope Envelope { get; }
        }

        private readonly object _notificationLock = new();
        private readonly object _clientLock = new();
        private readonly object _sendLock = new();
        private readonly Queue<Notification> _notifications = new();
        private readonly string _runtimeSessionId;
        private readonly IPAddress _listenAddress;
        private readonly int _port;
        private readonly int _fallbackPortCount;
        private readonly bool _requiresAccessToken;

        private TcpListener _listener;
        private TcpClient _client;
        private Thread _worker;
        private volatile bool _running;

        public RuntimeTcpServerTransport(string runtimeSessionId, IPAddress listenAddress, int port, bool requiresAccessToken, int fallbackPortCount = 0)
        {
            _runtimeSessionId = runtimeSessionId;
            _listenAddress = listenAddress ?? throw new ArgumentNullException(nameof(listenAddress));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            _port = port;
            _fallbackPortCount = Math.Min(32, Math.Max(0, fallbackPortCount));
            _requiresAccessToken = requiresAccessToken;
        }

        public event Action<RemoteEnvelope> MessageReceived;
        public event Action<int> EditorConnected;
        public event Action<int> EditorDisconnected;
        public bool RequiresAccessToken => _requiresAccessToken;
        public bool IsListening => _running && _listener != null && BoundPort > 0;
        public int ConfiguredPort => _port;
        public int BoundPort { get; private set; }

        public void Start()
        {
            if (_running)
                return;

            int finalPort = (int)Math.Min(65535L, (long)_port + _fallbackPortCount);
            Exception lastException = null;
            for (int candidatePort = _port; candidatePort <= finalPort; candidatePort++)
            {
                TcpListener candidate = null;
                try
                {
                    candidate = new TcpListener(_listenAddress, candidatePort);
                    candidate.Start(1);
                    _listener = candidate;
                    BoundPort = candidatePort;
                    _running = true;
                    _worker = new Thread(WorkerLoop)
                    {
                        IsBackground = true,
                        Name = "Remote Dev Utilities TCP"
                    };
                    _worker.Start();

                    if (candidatePort == _port)
                    {
                        Debug.Log($"[RemoteDevUtilities] ENABLE_DEBUG TCP transport listening on " + $"{_listenAddress}:{candidatePort}.");
                    }
                    else
                    {
                        Debug.LogWarning($"[RemoteDevUtilities] Configured TCP port {_port} is already in use. " +
                                         $"The ENABLE_DEBUG transport selected {_listenAddress}:{candidatePort}. " +
                                         $"LAN discovery will advertise port {candidatePort} when enabled; use that port for Direct IP.");
                    }

                    return;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    _running = false;
                    _listener = null;
                    BoundPort = 0;
                    _worker = null;
                    try
                    {
                        candidate?.Stop();
                    }
                    catch (SocketException)
                    {
                    }

                    if (!IsAddressAlreadyInUse(exception))
                        break;
                }
            }

            _running = false;
            BoundPort = 0;
            StopListener();
            LogStartFailure(lastException ?? new InvalidOperationException("No TCP port candidate could be started."), finalPort);
        }

        private void LogStartFailure(Exception exception, int finalPort)
        {
            string listenScope = IPAddress.Any.Equals(_listenAddress) ? "All network interfaces" : IPAddress.Loopback.Equals(_listenAddress) ? "Loopback only" : "Specific interface";
            string portDetails = finalPort == _port ? _port.ToString() : $"{_port}-{finalPort}";
            string commonDetails = $"Endpoint={_listenAddress}, PortsTried={portDetails}, " + $"Scope={listenScope}, " + $"Platform={Application.platform}, " + $"Unity={Application.unityVersion}";

            if (exception is SocketException socketException)
            {
                Debug.LogError($"[RemoteDevUtilities] Could not start the ENABLE_DEBUG TCP transport. " + $"{commonDetails}, " + $"SocketError={socketException.SocketErrorCode}, " + $"NativeErrorCode={socketException.NativeErrorCode}, " + $"ErrorCode={socketException.ErrorCode}. " + $"Message: {socketException.Message}");
                return;
            }

            Debug.LogError($"[RemoteDevUtilities] Could not start the ENABLE_DEBUG TCP transport. " + $"{commonDetails}, " + $"Exception={exception.GetType().FullName}. " + $"Message: {exception.Message}");
        }

        private void StopListener()
        {
            TcpListener listener = _listener;
            _listener = null;
            try
            {
                listener?.Stop();
            }
            catch (SocketException)
            {
            }
        }

        public void Tick()
        {
            while (TryDequeue(out Notification notification))
            {
                switch (notification.Type)
                {
                    case NotificationType.Connected:
                        EditorConnected?.Invoke(0);
                        break;
                    case NotificationType.Disconnected:
                        EditorDisconnected?.Invoke(0);
                        break;
                    case NotificationType.Message:
                        MessageReceived?.Invoke(notification.Envelope);
                        break;
                }
            }
        }

        public void Send<T>(string messageType, long requestId, T payload)
        {
            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, _runtimeSessionId, payload);

            lock (_sendLock)
            {
                TcpClient client;
                lock (_clientLock)
                    client = _client;
                if (client == null || !client.Connected)
                    return;

                try
                {
                    RemoteTcpFrameProtocol.WriteFrame(client.GetStream(), data);
                }
                catch (Exception exception) when (exception is IOException || exception is ObjectDisposedException || exception is SocketException)
                {
                    CloseClient(client);
                }
            }
        }

        public void Dispose()
        {
            _running = false;
            StopListener();
            BoundPort = 0;

            TcpClient client;
            lock (_clientLock)
            {
                client = _client;
                _client = null;
            }

            CloseClient(client);

            Thread worker = _worker;
            _worker = null;
            if (worker != null && worker != Thread.CurrentThread && worker.IsAlive)
                worker.Join(500);

            lock (_notificationLock)
                _notifications.Clear();

            MessageReceived = null;
            EditorConnected = null;
            EditorDisconnected = null;
        }

        private void WorkerLoop()
        {
            while (_running)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    lock (_clientLock)
                        _client = client;
                    Enqueue(new Notification(NotificationType.Connected));

                    NetworkStream stream = client.GetStream();
                    while (_running)
                    {
                        byte[] data = RemoteTcpFrameProtocol.ReadFrame(stream);
                        if (data == null)
                            break;
                        if (RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out _))
                        {
                            Enqueue(new Notification(NotificationType.Message, envelope));
                        }
                    }
                }
                catch (Exception exception) when (!_running || exception is IOException || exception is ObjectDisposedException || exception is SocketException)
                {
                }
                finally
                {
                    bool wasActive;
                    lock (_clientLock)
                    {
                        wasActive = client != null && ReferenceEquals(_client, client);
                        if (wasActive)
                            _client = null;
                    }

                    CloseClient(client);
                    if (wasActive)
                        Enqueue(new Notification(NotificationType.Disconnected));
                }
            }
        }

        private void Enqueue(Notification notification)
        {
            lock (_notificationLock)
                _notifications.Enqueue(notification);
        }

        private bool TryDequeue(out Notification notification)
        {
            lock (_notificationLock)
            {
                if (_notifications.Count == 0)
                {
                    notification = default;
                    return false;
                }

                notification = _notifications.Dequeue();
                return true;
            }
        }

        private static void CloseClient(TcpClient client)
        {
            if (client == null)
                return;

            try
            {
                client.Close();
            }
            catch (SocketException)
            {
            }
        }

        private static bool IsAddressAlreadyInUse(Exception exception)
        {
            return exception is SocketException socketException && socketException.SocketErrorCode == SocketError.AddressAlreadyInUse;
        }
    }
}

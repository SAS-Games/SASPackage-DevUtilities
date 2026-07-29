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
    internal sealed class RuntimeTcpServerTransport : IRuntimeRemoteTransport
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
        private readonly bool _requiresAccessToken;

        private TcpListener _listener;
        private TcpClient _client;
        private Thread _worker;
        private volatile bool _running;

        public RuntimeTcpServerTransport(
            string runtimeSessionId,
            IPAddress listenAddress,
            int port,
            bool requiresAccessToken)
        {
            _runtimeSessionId = runtimeSessionId;
            _listenAddress = listenAddress ?? throw new ArgumentNullException(nameof(listenAddress));
            _port = port;
            _requiresAccessToken = requiresAccessToken;
        }

        public event Action<RemoteEnvelope> MessageReceived;
        public event Action<int> EditorConnected;
        public event Action<int> EditorDisconnected;
        public bool RequiresAccessToken => _requiresAccessToken;

        public void Start()
        {
            if (_running)
                return;

            try
            {
                _listener = new TcpListener(_listenAddress, _port);
                _listener.Start(1);
                _running = true;
                _worker = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "Remote Dev Utilities TCP"
                };
                _worker.Start();
                Debug.Log(
                    $"[RemoteDevUtilities] ENABLE_DEBUG TCP transport listening on " +
                    $"{_listenAddress}:{_port}.");
            }
            catch (Exception exception)
            {
                _running = false;
                StopListener();
                LogStartFailure(exception);
            }
        }

        private void LogStartFailure(Exception exception)
        {
            string listenScope = IPAddress.Any.Equals(_listenAddress)
                ? "All network interfaces"
                : IPAddress.Loopback.Equals(_listenAddress)
                    ? "Loopback only"
                    : "Specific interface";
            string commonDetails =
                $"Endpoint={_listenAddress}:{_port}, " +
                $"Scope={listenScope}, " +
                $"Platform={Application.platform}, " +
                $"Unity={Application.unityVersion}";

            if (exception is SocketException socketException)
            {
                Debug.LogError(
                    $"[RemoteDevUtilities] Could not start the ENABLE_DEBUG TCP transport. " +
                    $"{commonDetails}, " +
                    $"SocketError={socketException.SocketErrorCode}, " +
                    $"NativeErrorCode={socketException.NativeErrorCode}, " +
                    $"ErrorCode={socketException.ErrorCode}. " +
                    $"Message: {socketException.Message}");
                return;
            }

            Debug.LogError(
                $"[RemoteDevUtilities] Could not start the ENABLE_DEBUG TCP transport. " +
                $"{commonDetails}, " +
                $"Exception={exception.GetType().FullName}. " +
                $"Message: {exception.Message}");
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
            byte[] data = RemoteProtocolSerializer.Serialize(
                messageType,
                requestId,
                _runtimeSessionId,
                payload);

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
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is ObjectDisposedException ||
                    exception is SocketException)
                {
                    CloseClient(client);
                }
            }
        }

        public void Dispose()
        {
            _running = false;
            StopListener();

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
                        if (RemoteProtocolSerializer.TryDeserializeEnvelope(
                                data,
                                out RemoteEnvelope envelope,
                                out _))
                        {
                            Enqueue(new Notification(NotificationType.Message, envelope));
                        }
                    }
                }
                catch (Exception exception) when (
                    !_running ||
                    exception is IOException ||
                    exception is ObjectDisposedException ||
                    exception is SocketException)
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
    }
}

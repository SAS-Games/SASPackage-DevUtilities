using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RemoteDevUtilities.Transport.Tcp;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection.Tcp
{
    internal sealed class EditorTcpConnectionTransport : IDisposable
    {
        private enum NotificationType
        {
            Connected,
            Disconnected,
            Error,
            Message
        }

        private readonly struct Notification
        {
            public Notification(NotificationType type, string error = null, RemoteEnvelope envelope = null)
            {
                Type = type;
                Error = error;
                Envelope = envelope;
            }

            public NotificationType Type { get; }
            public string Error { get; }
            public RemoteEnvelope Envelope { get; }
        }

        private readonly object _notificationLock = new();
        private readonly object _clientLock = new();
        private readonly object _sendLock = new();
        private readonly Queue<Notification> _notifications = new();

        private TcpClient _client;
        private Thread _worker;
        private volatile bool _running;

        public event Action Connected;
        public event Action<string> Disconnected;
        public event Action<string> ConnectionFailed;
        public event Action<RemoteEnvelope> MessageReceived;

        public bool IsConnected
        {
            get
            {
                lock (_clientLock)
                    return _client != null && _client.Connected;
            }
        }

        public void Connect(string host, int port)
        {
            Disconnect();
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("A TCP host is required.", nameof(host));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            _running = true;
            _worker = new Thread(() => WorkerLoop(host.Trim(), port))
            {
                IsBackground = true,
                Name = "Remote Dev Utilities Editor TCP"
            };
            _worker.Start();
        }

        public void Tick()
        {
            while (TryDequeue(out Notification notification))
            {
                switch (notification.Type)
                {
                    case NotificationType.Connected:
                        Connected?.Invoke();
                        break;
                    case NotificationType.Disconnected:
                        Disconnected?.Invoke(notification.Error);
                        break;
                    case NotificationType.Error:
                        ConnectionFailed?.Invoke(notification.Error);
                        break;
                    case NotificationType.Message:
                        MessageReceived?.Invoke(notification.Envelope);
                        break;
                }
            }
        }

        public void Send<T>(string messageType, long requestId, string editorSessionId, T payload)
        {
            byte[] data = RemoteProtocolSerializer.Serialize(messageType, requestId, editorSessionId, payload);

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

        public void Disconnect()
        {
            _running = false;

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
        }

        public void Dispose()
        {
            Disconnect();
            Connected = null;
            Disconnected = null;
            ConnectionFailed = null;
            MessageReceived = null;
        }

        private void WorkerLoop(string host, int port)
        {
            var client = new TcpClient();
            try
            {
                lock (_clientLock)
                {
                    if (!_running)
                        return;
                    _client = client;
                }

                client.NoDelay = true;
                client.Connect(host, port);
                if (!_running)
                    return;

                Enqueue(new Notification(NotificationType.Connected));
                NetworkStream stream = client.GetStream();
                while (_running)
                {
                    byte[] data = RemoteTcpFrameProtocol.ReadFrame(stream);
                    if (data == null)
                        break;
                    if (RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out _))
                    {
                        Enqueue(new Notification(NotificationType.Message, envelope: envelope));
                    }
                }

                if (_running)
                {
                    Enqueue(new Notification(NotificationType.Disconnected, "The TCP connection to the Player closed."));
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ObjectDisposedException || exception is SocketException)
            {
                if (_running)
                {
                    bool hadConnected = client.Connected;
                    Enqueue(new Notification(hadConnected ? NotificationType.Disconnected : NotificationType.Error, exception.Message));
                }
            }
            finally
            {
                lock (_clientLock)
                {
                    if (ReferenceEquals(_client, client))
                        _client = null;
                }

                CloseClient(client);
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

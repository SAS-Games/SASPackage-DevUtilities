using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteEditorConnectionService("lan-discovery", 300)]
    internal sealed class EditorLanDiscoveryService : IRemoteLanDiscoveryService
    {
        private readonly struct Datagram
        {
            public Datagram(string host, byte[] data)
            {
                Host = host;
                Data = data;
            }

            public string Host { get; }
            public byte[] Data { get; }
        }

        private readonly object _gate = new();
        private readonly Queue<Datagram> _pending = new();
        private readonly RemoteLanDiscoveryRegistry _registry = new();
        private UdpClient _listener;
        private Thread _worker;
        private volatile bool _running;

        public IReadOnlyList<RemoteLanPlayerDescriptor> Players => _registry.Players;
        public string Error { get; private set; }

        public void Start(RemoteDevUtilitiesClient client) => Start();

        public void Start()
        {
            if (_running)
                return;

            try
            {
                var listener = new UdpClient(AddressFamily.InterNetwork);
                listener.ExclusiveAddressUse = false;
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, RemoteLanDiscoveryConstants.Port));
                listener.Client.ReceiveTimeout = 500;
                _listener = listener;
                _running = true;
                _worker = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "Remote Dev Utilities LAN Discovery"
                };
                _worker.Start();
                Error = null;
            }
            catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
            {
                Error = $"LAN discovery could not listen on UDP port {RemoteLanDiscoveryConstants.Port}: {exception.Message}";
                DisposeListener();
            }
        }

        public bool Tick(double now)
        {
            bool changed = false;
            while (TryDequeue(out Datagram datagram))
            {
                if (RemoteLanDiscoveryProtocol.TryDeserialize(datagram.Data, out RemoteLanDiscoveryBeacon beacon))
                    changed |= _registry.Accept(datagram.Host, beacon, now);
            }

            return _registry.RemoveExpired(now) || changed;
        }

        public bool Clear() => _registry.Clear();

        public void Dispose()
        {
            _running = false;
            DisposeListener();
            if (_worker != null && _worker.IsAlive) _worker.Join(750);
            _worker = null;
            lock (_gate)
                _pending.Clear();
            _registry.Clear();
        }

        private void ReceiveLoop()
        {
            UdpClient listener = _listener;
            while (_running)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = listener.Receive(ref endpoint);
                    lock (_gate)
                        _pending.Enqueue(new Datagram(endpoint.Address.ToString(), data));
                }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.TimedOut || !_running)
                {
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!_running)
                        return;
                }
            }
        }

        private bool TryDequeue(out Datagram datagram)
        {
            lock (_gate)
            {
                if (_pending.Count > 0)
                {
                    datagram = _pending.Dequeue();
                    return true;
                }
            }

            datagram = default;
            return false;
        }

        private void DisposeListener()
        {
            _listener?.Close();
            _listener = null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteEditorConnectionService("lan-discovery", 300)]
    internal sealed class EditorLanDiscoveryService : IRemoteLanDiscoveryService
    {
        private const double DiagnosticLogIntervalSeconds = 30d;

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
        private readonly Func<bool> _diagnosticsEnabled;
        private UdpClient _listener;
        private Thread _worker;
        private volatile bool _running;
        private double _nextReceiveDiagnosticLogTime;
        private double _nextRejectedDiagnosticLogTime;

        public IReadOnlyList<RemoteLanPlayerDescriptor> Players => _registry.Players;
        public string Error { get; private set; }

        public EditorLanDiscoveryService() : this(() =>
            RemoteDevUtilitiesProjectSettings.instance.Runtime.EnableLanDiscoveryDiagnosticLogs)
        {
        }

        internal EditorLanDiscoveryService(Func<bool> diagnosticsEnabled)
        {
            _diagnosticsEnabled = diagnosticsEnabled ?? throw new ArgumentNullException(
                nameof(diagnosticsEnabled));
        }

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
                _nextReceiveDiagnosticLogTime = 0d;
                _nextRejectedDiagnosticLogTime = 0d;
                if (DiagnosticsEnabled)
                {
                    Debug.Log($"[RemoteDevUtilities] LAN discovery listener started on 0.0.0.0:" +
                              $"{RemoteLanDiscoveryConstants.Port}/UDP. Waiting for Player beacons.");
                }
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
                {
                    bool beaconChanged = _registry.Accept(datagram.Host, beacon, now);
                    changed |= beaconChanged;
                    if (DiagnosticsEnabled &&
                        (beaconChanged || now >= _nextReceiveDiagnosticLogTime))
                    {
                        _nextReceiveDiagnosticLogTime = now + DiagnosticLogIntervalSeconds;
                        Debug.Log($"[RemoteDevUtilities] LAN discovery beacon received from " +
                                  $"{datagram.Host}. UDP bytes={datagram.Data.Length}, " +
                                  $"advertised TCP port={beacon.TcpPort}, session={beacon.RuntimeSessionId}, " +
                                  $"protocol={beacon.ProtocolVersion}, package={beacon.PackageVersion}, " +
                                  $"device={beacon.Target?.DeviceName ?? "unknown"}.");
                    }
                }
                else if (DiagnosticsEnabled && now >= _nextRejectedDiagnosticLogTime)
                {
                    _nextRejectedDiagnosticLogTime = now + DiagnosticLogIntervalSeconds;
                    Debug.LogWarning($"[RemoteDevUtilities] UDP datagram received on discovery port from " +
                                     $"{datagram.Host}, but it was not a valid Remote Dev Utilities beacon. " +
                                     $"UDP bytes={datagram.Data?.Length ?? 0}.");
                }
            }

            return _registry.RemoveExpired(now) || changed;
        }

        public bool Clear()
        {
            bool changed = _registry.Clear();
            if (DiagnosticsEnabled)
            {
                Debug.Log("[RemoteDevUtilities] LAN discovery results cleared. The UDP listener remains active; " +
                          "Search Again is passive and waits for the Player's next beacon.");
            }
            return changed;
        }

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

        private bool DiagnosticsEnabled => _diagnosticsEnabled();
    }
}

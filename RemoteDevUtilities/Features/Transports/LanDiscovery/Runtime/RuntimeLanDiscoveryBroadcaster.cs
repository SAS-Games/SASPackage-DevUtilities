using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    [Preserve]
    [RuntimeRemoteConnectionService("lan-discovery", 300)]
    internal sealed class RuntimeLanDiscoveryBroadcaster : IRuntimeRemoteConnectionService
    {
        private const double DiagnosticLogIntervalSeconds = 30d;

        // Ensure this optional service assembly is loaded before the core scans
        // AppDomain assemblies. Without a runtime root, console IL2CPP players
        // can omit the broadcaster even though its type has [Preserve].
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureRuntimeAssemblyIsLoaded()
        {
        }

        private byte[] _payload;
        private List<IPEndPoint> _destinations;
        private UdpClient _client;
        private int _advertisedTcpPort;
        private double _nextBeaconTime;
        private double _nextDiagnosticLogTime;
        private long _beaconSequence;
        private bool _reportedSendFailure;
        private bool _diagnosticsEnabled;

        public void Initialize(RuntimeRemoteConnectionServiceContext context)
        {
#if ENABLE_DEBUG && !UNITY_EDITOR && !UNITY_WEBGL
            IRuntimeTcpEndpoint tcpEndpoint = null;
            IReadOnlyList<IRuntimeRemoteTransport> transports = context?.Transports;
            for (int i = 0; transports != null && i < transports.Count; i++)
            {
                if (transports[i] is IRuntimeTcpEndpoint endpoint)
                {
                    tcpEndpoint = endpoint;
                    break;
                }
            }

            RemoteDevUtilitiesRuntimeSettings settings = context?.Settings;
            if (settings == null || !settings.EnableLanDiscovery)
                return;

            _diagnosticsEnabled = settings.EnableLanDiscoveryDiagnosticLogs;

            if (tcpEndpoint?.IsListening != true)
            {
                Debug.LogWarning("[RemoteDevUtilities] LAN discovery is enabled, but the TCP endpoint is not listening. " +
                                 "No UDP discovery beacons will be sent.");
                return;
            }

            if (!settings.AllowTcpConnectionsFromOtherMachines)
            {
                Debug.LogWarning("[RemoteDevUtilities] LAN discovery is enabled, but LAN TCP access is disabled. " +
                                 "No UDP discovery beacons will be sent.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.TcpAccessToken))
            {
                Debug.LogWarning("[RemoteDevUtilities] LAN discovery is enabled, but the TCP access token is empty. " +
                                 "No UDP discovery beacons will be sent.");
                return;
            }

            _advertisedTcpPort = tcpEndpoint.BoundPort;
            _payload = RemoteLanDiscoveryProtocol.Serialize(new RemoteLanDiscoveryBeacon
            {
                Signature = RemoteLanDiscoveryConstants.Signature,
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                RuntimeSessionId = context.RuntimeSessionId,
                TcpPort = _advertisedTcpPort,
                Target = RuntimeConnectionEndpoint.CreateTargetDescriptor()
            });
            _destinations = BuildDestinations();
            Start();
#endif
        }

        public void Start()
        {
            try
            {
                _client = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                _nextBeaconTime = 0d;
                _nextDiagnosticLogTime = 0d;
                _beaconSequence = 0;
                if (_diagnosticsEnabled)
                {
                    Debug.Log($"[RemoteDevUtilities] LAN discovery broadcaster started. " +
                              $"UDP port={RemoteLanDiscoveryConstants.Port}, TCP port={_advertisedTcpPort}, " +
                              $"interval={RemoteLanDiscoveryConstants.BeaconIntervalSeconds:0.###}s, " +
                              $"destinations=[{FormatDestinations()}].");
                }
            }
            catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
            {
                Debug.LogWarning("[RemoteDevUtilities] LAN discovery could not start: " + exception.Message);
                Dispose();
            }
        }

        public void Tick()
        {
            if (_client == null || _payload == null || _destinations == null || Time.realtimeSinceStartupAsDouble < _nextBeaconTime)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            _nextBeaconTime = now + RemoteLanDiscoveryConstants.BeaconIntervalSeconds;
            _beaconSequence++;
            int successfulDestinations = 0;
            for (int i = 0; i < _destinations.Count; i++)
            {
                try
                {
                    _client.Send(_payload, _payload.Length, _destinations[i]);
                    successfulDestinations++;
                }
                catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
                {
                    if (!_reportedSendFailure)
                    {
                        _reportedSendFailure = true;
                        Debug.LogWarning($"[RemoteDevUtilities] LAN discovery beacon could not be sent to " +
                                         $"{_destinations[i]}: {exception.Message}");
                    }
                }
            }

            if (successfulDestinations > 0)
            {
                _reportedSendFailure = false;
                if (_diagnosticsEnabled && now >= _nextDiagnosticLogTime)
                {
                    _nextDiagnosticLogTime = now + DiagnosticLogIntervalSeconds;
                    Debug.Log($"[RemoteDevUtilities] LAN discovery beacon #{_beaconSequence} sent to " +
                              $"{successfulDestinations}/{_destinations.Count} destination(s). " +
                              $"UDP bytes={_payload.Length}, advertised TCP port={_advertisedTcpPort}.");
                }
            }
        }

        public void Dispose()
        {
            _client?.Close();
            _client = null;
        }

        private static List<IPEndPoint> BuildDestinations()
        {
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            var destinations = new List<IPEndPoint>();
            AddDestination(IPAddress.Broadcast, addresses, destinations);

            try
            {
                foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    foreach (UnicastIPAddressInformation unicast in network.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null)
                            continue;

                        byte[] address = unicast.Address.GetAddressBytes();
                        byte[] mask = unicast.IPv4Mask.GetAddressBytes();
                        var broadcast = new byte[address.Length];
                        for (int i = 0; i < broadcast.Length; i++)
                            broadcast[i] = (byte)(address[i] | ~mask[i]);
                        AddDestination(new IPAddress(broadcast), addresses, destinations);
                    }
                }
            }
            catch (Exception exception) when (exception is NetworkInformationException || exception is SocketException)
            {
                // The limited broadcast destination above remains available.
            }

            return destinations;
        }

        private static void AddDestination(IPAddress address, HashSet<string> addresses, List<IPEndPoint> destinations)
        {
            string key = address.ToString();
            if (addresses.Add(key))
                destinations.Add(new IPEndPoint(address, RemoteLanDiscoveryConstants.Port));
        }

        private string FormatDestinations()
        {
            if (_destinations == null || _destinations.Count == 0)
                return "none";

            var result = new StringBuilder();
            for (int i = 0; i < _destinations.Count; i++)
            {
                if (i > 0)
                    result.Append(", ");
                result.Append(_destinations[i]);
            }
            return result.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEngine;
using UnityEngine.Scripting;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    [Preserve]
    [RuntimeRemoteConnectionService("lan-discovery", 300)]
    internal sealed class RuntimeLanDiscoveryBroadcaster : IRuntimeRemoteConnectionService
    {
        private byte[] _payload;
        private List<IPEndPoint> _destinations;
        private UdpClient _client;
        private double _nextBeaconTime;
        private bool _reportedSendFailure;

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
            if (tcpEndpoint?.IsListening != true || settings == null || !settings.EnableLanDiscovery ||
                !settings.AllowTcpConnectionsFromOtherMachines || string.IsNullOrWhiteSpace(settings.TcpAccessToken))
                return;

            _payload = RemoteLanDiscoveryProtocol.Serialize(new RemoteLanDiscoveryBeacon
            {
                Signature = RemoteLanDiscoveryConstants.Signature,
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                RuntimeSessionId = context.RuntimeSessionId,
                TcpPort = tcpEndpoint.BoundPort,
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

            _nextBeaconTime = Time.realtimeSinceStartupAsDouble + RemoteLanDiscoveryConstants.BeaconIntervalSeconds;
            bool sent = false;
            for (int i = 0; i < _destinations.Count; i++)
            {
                try
                {
                    _client.Send(_payload, _payload.Length, _destinations[i]);
                    sent = true;
                }
                catch (Exception exception) when (exception is SocketException || exception is ObjectDisposedException)
                {
                    if (!_reportedSendFailure)
                    {
                        _reportedSendFailure = true;
                        Debug.LogWarning("[RemoteDevUtilities] LAN discovery beacon could not be sent: " + exception.Message);
                    }
                }
            }

            if (sent)
                _reportedSendFailure = false;
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
    }
}

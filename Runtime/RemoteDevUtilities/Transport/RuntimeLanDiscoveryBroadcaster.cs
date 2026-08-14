using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    internal sealed class RuntimeLanDiscoveryBroadcaster : IDisposable
    {
        private readonly byte[] _payload;
        private readonly List<IPEndPoint> _destinations;
        private UdpClient _client;
        private double _nextBeaconTime;
        private bool _reportedSendFailure;

        private RuntimeLanDiscoveryBroadcaster(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings, int tcpPort)
        {
            _payload = RemoteLanDiscoveryProtocol.Serialize(new RemoteLanDiscoveryBeacon
            {
                Signature = RemoteProtocolConstants.LanDiscoverySignature,
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                RuntimeSessionId = runtimeSessionId,
                TcpPort = tcpPort,
                Target = RuntimeConnectionEndpoint.CreateTargetDescriptor()
            });
            _destinations = BuildDestinations();
        }

        public static RuntimeLanDiscoveryBroadcaster TryCreate(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings, int tcpPort)
        {
#if ENABLE_DEBUG && !UNITY_EDITOR && !UNITY_WEBGL
            if (settings == null || tcpPort < 1 || tcpPort > 65535 || !settings.EnableLanDiscovery || !settings.AllowTcpConnectionsFromOtherMachines || string.IsNullOrWhiteSpace(settings.TcpAccessToken))
                return null;

            return new RuntimeLanDiscoveryBroadcaster(runtimeSessionId, settings, tcpPort);
#else
            return null;
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
            if (_client == null || Time.realtimeSinceStartupAsDouble < _nextBeaconTime)
                return;

            _nextBeaconTime = Time.realtimeSinceStartupAsDouble + RemoteProtocolConstants.LanDiscoveryBeaconIntervalSeconds;
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
                destinations.Add(new IPEndPoint(address, RemoteProtocolConstants.LanDiscoveryPort));
        }
    }
}

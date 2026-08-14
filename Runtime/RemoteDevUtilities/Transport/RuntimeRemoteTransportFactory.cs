using System.Net;
using SAS.Utilities.RemoteDevUtilities.Transport.Tcp;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    internal static class RuntimeRemoteTransportFactory
    {
        public static IRuntimeRemoteTransport Create(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings, out IRuntimeTcpEndpoint tcpEndpoint)
        {
#if ENABLE_DEBUG && DEVELOPMENT_BUILD && !UNITY_EDITOR && !UNITY_WEBGL
            RuntimeTcpServerTransport tcpTransport = CreateTcpTransport(runtimeSessionId, settings);
            tcpEndpoint = tcpTransport;
            return new RuntimeMultiplexedTransport(
                new RuntimePlayerConnectionTransport(runtimeSessionId),
                tcpTransport);
#elif ENABLE_DEBUG && !DEVELOPMENT_BUILD && !UNITY_EDITOR && !UNITY_WEBGL
            RuntimeTcpServerTransport tcpTransport = CreateTcpTransport(runtimeSessionId, settings);
            tcpEndpoint = tcpTransport;
            return tcpTransport;
#else
            tcpEndpoint = null;
            return new RuntimePlayerConnectionTransport(runtimeSessionId);
#endif
        }

        private static RuntimeTcpServerTransport CreateTcpTransport(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings)
        {
            bool allowLan = settings.AllowTcpConnectionsFromOtherMachines;
            if (allowLan && string.IsNullOrWhiteSpace(settings.TcpAccessToken))
            {
                Debug.LogWarning("[RemoteDevUtilities] LAN TCP access requires a non-empty access token. " + "The ENABLE_DEBUG transport will listen on loopback only.");
                allowLan = false;
            }

            return new RuntimeTcpServerTransport(
                runtimeSessionId,
                allowLan ? IPAddress.Any : IPAddress.Loopback,
                settings.TcpPort,
                !string.IsNullOrWhiteSpace(settings.TcpAccessToken),
                settings.TcpPortFallbackCount);
        }
    }
}

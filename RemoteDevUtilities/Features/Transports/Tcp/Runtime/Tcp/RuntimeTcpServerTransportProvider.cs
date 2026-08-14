using System.Net;
using UnityEngine;
using UnityEngine.Scripting;

namespace SAS.Utilities.RemoteDevUtilities.Transport.Tcp
{
    [Preserve]
    [RuntimeRemoteTransportProvider("tcp", 200)]
    internal sealed class RuntimeTcpServerTransportProvider : IRuntimeRemoteTransportProvider
    {
        public IRuntimeRemoteTransport Create(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings)
        {
#if ENABLE_DEBUG && !UNITY_EDITOR && !UNITY_WEBGL
            bool allowLan = settings.AllowTcpConnectionsFromOtherMachines;
            if (allowLan && string.IsNullOrWhiteSpace(settings.TcpAccessToken))
            {
                Debug.LogWarning("[RemoteDevUtilities] LAN TCP access requires a non-empty access token. " +
                                 "The ENABLE_DEBUG transport will listen on loopback only.");
                allowLan = false;
            }

            return new RuntimeTcpServerTransport(
                runtimeSessionId,
                allowLan ? IPAddress.Any : IPAddress.Loopback,
                settings.TcpPort,
                !string.IsNullOrWhiteSpace(settings.TcpAccessToken),
                settings.TcpPortFallbackCount);
#else
            return null;
#endif
        }
    }
}

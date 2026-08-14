using UnityEngine.Scripting;

namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    [Preserve]
    [RuntimeRemoteTransportProvider("player-connection", 100)]
    internal sealed class RuntimePlayerConnectionTransportProvider : IRuntimeRemoteTransportProvider
    {
        public IRuntimeRemoteTransport Create(string runtimeSessionId, RemoteDevUtilitiesRuntimeSettings settings)
        {
#if ENABLE_DEBUG && !DEVELOPMENT_BUILD && !UNITY_EDITOR && !UNITY_WEBGL
            return null;
#else
            return new RuntimePlayerConnectionTransport(runtimeSessionId);
#endif
        }
    }
}

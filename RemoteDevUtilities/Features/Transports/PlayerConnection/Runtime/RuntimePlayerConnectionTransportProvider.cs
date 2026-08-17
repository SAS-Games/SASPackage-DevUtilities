using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace HP.Utilities.RemoteDevUtilities.Transport
{
    [Preserve]
    [RuntimeRemoteTransportProvider("player-connection", 100)]
    internal sealed class RuntimePlayerConnectionTransportProvider : IRuntimeRemoteTransportProvider
    {
        // The core discovers providers from the assemblies currently loaded in
        // the Player.  A split transport assembly has no ordinary call site, so
        // IL2CPP players can otherwise leave it unloaded (or strip it) before
        // discovery runs.  This callback is an explicit player-side root and
        // runs before RuntimeDevUtilitiesAgentBootstrap.Spawn.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureRuntimeAssemblyIsLoaded()
        {
        }

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

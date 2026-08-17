using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Agent
{
    internal static class RuntimeDevUtilitiesAgentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
#if !UNITY_EDITOR
#if ENABLE_DEBUG
            if (RuntimeDevUtilitiesAgent.Instance != null)
                return;

            RemoteDevUtilitiesRuntimeSettings settings =
                RemoteDevUtilitiesRuntimeSettings.LoadOrCreateDefaults();
            if (!settings.EnableRemoteAgent)
                return;

            var agentObject = new GameObject("[Remote Dev Utilities Agent]")
            {
                hideFlags = HideFlags.DontSave
            };
            agentObject.SetActive(false);
            RuntimeDevUtilitiesAgent agent =
                agentObject.AddComponent<RuntimeDevUtilitiesAgent>();
            agent.Initialize(settings);
            agentObject.SetActive(true);
            Object.DontDestroyOnLoad(agentObject);
#endif
#endif
        }
    }
}

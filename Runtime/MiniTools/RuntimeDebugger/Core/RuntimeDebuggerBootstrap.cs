using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    internal static class RuntimeDebuggerBootstrap
    {
#if ENABLE_DEBUG
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            RuntimeDebuggerSettings settings = RuntimeDebuggerSettings.LoadOrCreateDefaults();
            if (!settings.EnableDebugger || !settings.AutomaticallyCreateBootstrap ||
                RuntimeDebuggerHost.Instance != null) return;
            var host = new GameObject("[Runtime Debugger]") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<RuntimeDebuggerHost>().Initialize(settings);
        }
#endif
    }
}
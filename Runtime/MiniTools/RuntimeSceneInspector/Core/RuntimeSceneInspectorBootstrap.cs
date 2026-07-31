using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector
{
    internal static class RuntimeSceneInspectorBootstrap
    {
#if ENABLE_DEBUG
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            RuntimeSceneInspectorSettings settings = RuntimeSceneInspectorSettings.LoadOrCreateDefaults();
            if (!settings.EnableInspector || !settings.AutomaticallyCreateBootstrap ||
                RuntimeSceneInspectorHost.Instance != null) return;
            var host = new GameObject("[Runtime Scene Inspector]") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<RuntimeSceneInspectorHost>().Initialize(settings);
        }
#endif
    }
}
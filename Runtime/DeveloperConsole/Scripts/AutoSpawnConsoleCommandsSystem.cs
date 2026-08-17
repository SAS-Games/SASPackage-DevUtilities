using UnityEngine;

namespace HP.Utilities.DeveloperConsole
{
    public static class AutoSpawnConsoleCommandsSystem
    {
        internal static bool SuppressAutomaticSpawn { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
#if !ENABLE_DEBUG
            return;
#endif
            if (SuppressAutomaticSpawn)
                return;

            if (DeveloperConsoleBehaviour.Instance != null)
                return;

            var prefab = Resources.Load<GameObject>("ConsoleCommandsSystem");
            if (prefab == null)
            {
                Debug.LogError("[DeveloperConsole] Prefab not found");
                return;
            }

            Object.Instantiate(prefab);
        }
    }
}

using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public static class DeveloperConsoleAutoSpawn
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
#if !ENABLE_DEBUG
            return;
#endif
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

using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Commands
{
    internal static class RuntimeDeveloperConsoleProvider
    {
        private const string ConsoleResourceName = "ConsoleCommandsSystem";

        public static DeveloperConsoleBehaviour GetOrCreate()
        {
            DeveloperConsoleBehaviour behaviour = DeveloperConsoleBehaviour.Instance;
            if (behaviour == null)
                behaviour = CreateConsole();

            if (behaviour != null)
            {
                MiniToolRuntimeRegistry.RegisterCommands(behaviour.DeveloperConsole);
            }

            return behaviour;
        }

        private static DeveloperConsoleBehaviour CreateConsole()
        {
            GameObject prefab = Resources.Load<GameObject>(ConsoleResourceName);
            if (prefab == null)
                return null;

            GameObject instance = Object.Instantiate(prefab);
            instance.name = "[Remote Developer Console]";
            Object.DontDestroyOnLoad(instance);
            return instance.GetComponent<DeveloperConsoleBehaviour>() ?? instance.GetComponentInChildren<DeveloperConsoleBehaviour>(true);
        }
    }
}

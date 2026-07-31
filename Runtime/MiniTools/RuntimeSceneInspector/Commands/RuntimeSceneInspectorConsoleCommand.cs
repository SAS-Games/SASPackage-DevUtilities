using SAS.Utilities.RuntimeSceneInspector;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "Runtime Scene Inspector Command", menuName = DeveloperConsole.CommandBasePath + "Runtime Scene Inspector")]
    public sealed class RuntimeSceneInspectorConsoleCommand : ConsoleCommand
    {
        public override string HelpText =>
            "Usage: RuntimeSceneInspector <On|Off>. Enables or fully suspends the runtime scene inspector.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_DEBUG
            if (args == null || args.Length != 1)
                return false;

            RuntimeSceneInspectorHost host = RuntimeSceneInspectorHost.Instance;

            if (!BoolUtil.TryParse(args[0], out var enable))
                return false;

            if (enable)
            {
                host = RuntimeSceneInspectorHost.GetOrCreateEnabledHost();
                if (host == null)
                {
                    Debug.LogWarning("Runtime Scene Inspector is disabled by its settings asset.");
                    return false;
                }

                host.SetOverlayVisible(true);
            }
            else
            {
                host?.SetInspectorEnabled(false);
            }

            Debug.Log($"Runtime Scene Inspector {(enable ? "enabled" : "disabled")}.");
            return true;
#else
            Debug.LogWarning(
                "Runtime Scene Inspector is not available. Enable ENABLE_DEBUG or use a Development Build.");
            return false;
#endif
        }
    }
}

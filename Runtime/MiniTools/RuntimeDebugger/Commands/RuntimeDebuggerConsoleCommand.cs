using SAS.Utilities.RuntimeDebugger;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "Runtime Debugger Command", menuName = DeveloperConsole.CommandBasePath + "Runtime Debugger")]
    public sealed class RuntimeDebuggerConsoleCommand : ConsoleCommand
    {
        public override string HelpText =>
            "Usage: RuntimeDebugger <On|Off>. Enables or fully suspends the runtime debugger.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_DEBUG
            if (args == null || args.Length != 1)
                return false;

            RuntimeDebuggerHost host = RuntimeDebuggerHost.Instance;

            if (!BoolUtil.TryParse(args[0], out var enable))
                return false;

            if (enable)
            {
                host = RuntimeDebuggerHost.GetOrCreateEnabledHost();
                if (host == null)
                {
                    Debug.LogWarning("Runtime Debugger is disabled by its settings asset.");
                    return false;
                }

                host.SetOverlayVisible(true);
            }
            else
            {
                host?.SetDebuggerEnabled(false);
            }

            Debug.Log($"Runtime Debugger {(enable ? "enabled" : "disabled")}.");
            return true;
#else
            Debug.LogWarning(
                "Runtime Debugger is not available. Enable ENABLE_DEBUG or use a Development Build.");
            return false;
#endif
        }
    }
}

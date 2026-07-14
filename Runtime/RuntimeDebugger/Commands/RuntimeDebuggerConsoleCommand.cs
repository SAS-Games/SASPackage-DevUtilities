using System;
using SAS.Utilities.RuntimeDebugger;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "Runtime Debugger Command", menuName = DeveloperConsole.CommandBasePath + "Runtime Debugger")]
    public sealed class RuntimeDebuggerConsoleCommand : ConsoleCommand
    {
        public override string HelpText => "Usage: RuntimeDebugger <On|Off|Toggle|Status>. Enables or fully suspends the runtime debugger.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || SAS_RUNTIME_DEBUGGER
            if (args == null || args.Length != 1)
                return false;

            string operation = args[0];
            RuntimeDebuggerHost host = RuntimeDebuggerHost.Instance;
            if (operation.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                string state = host == null || !host.IsDebuggerEnabled ? "disabled" : host.IsOpen ? "enabled and open" : "enabled and closed";
                Debug.Log($"Runtime Debugger is {state}.");
                return true;
            }

            bool enable;
            if (operation.Equals("on", StringComparison.OrdinalIgnoreCase) || operation.Equals("enable", StringComparison.OrdinalIgnoreCase))
                enable = true;
            else if (operation.Equals("off", StringComparison.OrdinalIgnoreCase) || operation.Equals("disable", StringComparison.OrdinalIgnoreCase))
                enable = false;
            else if (operation.Equals("toggle", StringComparison.OrdinalIgnoreCase))
                enable = host == null || !host.IsDebuggerEnabled;
            else
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
            Debug.LogWarning("Runtime Debugger is not available in this release build.");
            return false;
#endif
        }
    }
}

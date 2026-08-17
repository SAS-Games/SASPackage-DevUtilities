using HP.Utilities.RemoteDevUtilities.Commands;
using UnityEngine.Scripting;
using RuntimeConsole = HP.Utilities.DeveloperConsole.DeveloperConsole;

namespace HP.Utilities.RemoteDevUtilities.MiniTools
{
    [Preserve]
    [RuntimeDeveloperConsoleContribution(300)]
    internal sealed class RemoteMiniToolConsoleContribution : IRuntimeDeveloperConsoleContribution
    {
        public void Configure(RuntimeConsole console) => MiniToolRuntimeRegistry.RegisterCommands(console);
    }
}

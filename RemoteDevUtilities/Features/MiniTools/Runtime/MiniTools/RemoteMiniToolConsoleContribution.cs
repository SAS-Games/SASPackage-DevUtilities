using SAS.Utilities.RemoteDevUtilities.Commands;
using UnityEngine.Scripting;
using RuntimeConsole = SAS.Utilities.DeveloperConsole.DeveloperConsole;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools
{
    [Preserve]
    [RuntimeDeveloperConsoleContribution(300)]
    internal sealed class RemoteMiniToolConsoleContribution : IRuntimeDeveloperConsoleContribution
    {
        public void Configure(DeveloperConsole.DeveloperConsole console) => MiniToolRuntimeRegistry.RegisterCommands(console);
    }
}

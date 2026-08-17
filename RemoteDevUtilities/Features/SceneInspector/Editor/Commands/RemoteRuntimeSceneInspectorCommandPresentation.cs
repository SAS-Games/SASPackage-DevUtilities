using System;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.DebugHost;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    [RemoteCommandPresentationHandler(400)]
    internal sealed class RemoteRuntimeSceneInspectorCommandPresentation : IRemoteCommandPresentationHandler
    {
        private const string CommandName = "RuntimeSceneInspector";

        public bool TryExecute(
            RemoteDevUtilitiesClient client,
            string commandName,
            string[] arguments,
            out RemoteCommandPresentationResult result)
        {
            result = default;
            if (!string.Equals(commandName, CommandName, StringComparison.OrdinalIgnoreCase) || RemoteDebugHostSession.RuntimeSceneInspector == null)
                return false;

            if (arguments == null || arguments.Length != 1 || !BoolUtil.TryParse(arguments[0], out bool visible))
            {
                result = RemoteCommandPresentationResult.Local(false, "Usage: RuntimeSceneInspector <On|Off>.");
                return true;
            }

            RemoteDebugHostSession.SetRuntimeSceneInspectorPresentationVisible(visible);
            result = RemoteCommandPresentationResult.Local(
                true,
                visible
                    ? "Runtime Scene Inspector opened in the Editor Debug Host."
                    : "Runtime Scene Inspector closed in the Editor Debug Host.");
            return true;
        }
    }
}

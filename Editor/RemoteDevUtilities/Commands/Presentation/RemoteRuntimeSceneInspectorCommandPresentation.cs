using System;
using SAS.Utilities.RemoteDevUtilities.DebugHost;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    internal static class RemoteRuntimeSceneInspectorCommandPresentation
    {
        private const string CommandName = "RuntimeSceneInspector";

        internal static bool TryExecute(RemoteDevUtilitiesClient client, string commandName, string[] arguments)
        {
            if (!string.Equals(commandName, CommandName, StringComparison.OrdinalIgnoreCase) || RemoteDebugHostSession.RuntimeSceneInspector == null)
                return false;

            if (arguments == null || arguments.Length != 1 || !RemoteCommandPresentationBinding.TryParseToggle(arguments, out bool visible))
            {
                Complete(client, false, "Usage: RuntimeSceneInspector <On|Off>.");
                return true;
            }

            RemoteDebugHostSession.SetRuntimeSceneInspectorPresentationVisible(visible);
            Complete(client, true, visible ? "Runtime Scene Inspector opened in the Editor Debug Host." : "Runtime Scene Inspector closed in the Editor Debug Host.");
            return true;
        }

        private static void Complete(RemoteDevUtilitiesClient client, bool success, string message)
        {
            client.Commands.CompleteLocally(new RemoteCommandExecuteResponse
                {
                    Success = success,
                    CloseRequested = false,
                    Message = message
                });
        }
    }
}

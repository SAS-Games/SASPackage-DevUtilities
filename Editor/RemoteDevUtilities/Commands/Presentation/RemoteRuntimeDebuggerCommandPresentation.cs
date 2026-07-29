using System;
using SAS.Utilities.RemoteDevUtilities.DebugHost;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    internal static class RemoteRuntimeDebuggerCommandPresentation
    {
        private const string CommandName = "RuntimeDebugger";

        internal static bool TryExecute(RemoteDevUtilitiesClient client, string commandName, string[] arguments)
        {
            if (!string.Equals(commandName, CommandName, StringComparison.OrdinalIgnoreCase) || RemoteDebugHostSession.RuntimeDebugger == null)
                return false;

            if (arguments == null || arguments.Length != 1 || !RemoteCommandPresentationBinding.TryParseToggle(arguments, out bool visible))
            {
                Complete(client, false, "Usage: RuntimeDebugger <On|Off>.");
                return true;
            }

            RemoteDebugHostSession.SetRuntimeDebuggerPresentationVisible(visible);
            Complete(client, true, visible ? "Runtime Debugger opened in the Editor Debug Host." : "Runtime Debugger closed in the Editor Debug Host.");
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

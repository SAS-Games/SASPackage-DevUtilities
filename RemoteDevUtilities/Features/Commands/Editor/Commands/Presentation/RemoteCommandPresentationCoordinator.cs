using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    internal sealed class RemoteCommandPresentationCoordinator
    {
        private static readonly char[] CommandSeparators = { ' ', '\t', '\r', '\n' };
        private readonly RemoteDevUtilitiesClient _client;
        private readonly RemoteCommandClient _commands;
        private readonly IReadOnlyList<IRemoteCommandPresentationHandler> _handlers;

        internal RemoteCommandPresentationCoordinator(RemoteDevUtilitiesClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _commands = client.GetRequiredFeature<RemoteCommandClient>();
            _handlers = RemoteCommandPresentationHandlerRegistry.CreateHandlers();
        }

        internal void Execute(string commandLine)
        {
            if (!TryParseCommandLine(commandLine, _commands.Prefix, out string commandName, out string[] arguments))
            {
                Complete(false, "Enter a command to execute.");
                return;
            }

            for (int i = 0; i < _handlers.Count; i++)
            {
                if (!_handlers[i].TryExecute(_client, commandName, arguments, out RemoteCommandPresentationResult result))
                    continue;
                if (result.ExecuteRemotely)
                    _commands.Execute(commandLine);
                else
                    Complete(result.Success, result.Message);
                return;
            }

            _commands.Execute(commandLine);
        }

        private void Complete(bool success, string message)
        {
            _commands.CompleteLocally(new RemoteCommandExecuteResponse
            {
                Success = success,
                CloseRequested = false,
                Message = message
            });
        }

        internal static bool TryParseCommandLine(string commandLine, string prefix, out string commandName, out string[] arguments)
        {
            commandName = null;
            arguments = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(commandLine))
                return false;

            string normalized = commandLine.Trim();
            if (!string.IsNullOrEmpty(prefix) && normalized.StartsWith(prefix, StringComparison.Ordinal))
                normalized = normalized.Substring(prefix.Length).TrimStart();

            string[] parts = normalized.Split(CommandSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            commandName = parts[0];
            if (parts.Length == 1)
                return true;

            arguments = new string[parts.Length - 1];
            Array.Copy(parts, 1, arguments, 0, arguments.Length);
            return true;
        }
    }
}

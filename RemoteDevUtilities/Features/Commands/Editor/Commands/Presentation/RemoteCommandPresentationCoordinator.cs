using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace HP.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
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

        internal long Execute(string commandLine)
        {
            if (!TryParseCommandLine(commandLine, _commands.Prefix, out string commandName, out string[] arguments))
            {
                return Complete(false, "Enter a command to execute.");
            }

            for (int i = 0; i < _handlers.Count; i++)
            {
                if (!_handlers[i].TryExecute(_client, commandName, arguments, out RemoteCommandPresentationResult result))
                    continue;
                if (result.ExecuteRemotely)
                    return _commands.Execute(commandLine);
                return Complete(result.Success, result.Message);
            }

            return _commands.Execute(commandLine);
        }

        private long Complete(bool success, string message)
        {
            _commands.CompleteLocally(new RemoteCommandExecuteResponse
            {
                Success = success,
                CloseRequested = false,
                Message = message
            });
            return 0;
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

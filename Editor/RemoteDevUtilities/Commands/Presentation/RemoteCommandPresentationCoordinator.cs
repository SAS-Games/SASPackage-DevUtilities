using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    internal sealed class RemoteCommandPresentationCoordinator
    {
        private static readonly char[] CommandSeparators = { ' ', '\t', '\r', '\n' };
        private readonly RemoteDevUtilitiesClient _client;

        internal RemoteCommandPresentationCoordinator(RemoteDevUtilitiesClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        internal void Execute(string commandLine)
        {
            if (!TryParseCommandLine(commandLine, _client.Commands.Prefix, out string commandName, out string[] arguments))
            {
                Complete(false, "Enter a command to execute.");
                return;
            }

            if (RemoteRuntimeDebuggerCommandPresentation.TryExecute(_client, commandName, arguments))
                return;

            if (!TryResolveBinding(
                    commandName,
                    out RemoteCommandPresentationBinding binding) ||
                binding.Routing == RemoteCommandRouting.ExecuteInBuildOnly)
            {
                _client.Commands.Execute(commandLine);
                return;
            }

            if (!_client.IsConnected)
            {
                Complete(false, "Connect to a runtime Player before executing this command.");
                return;
            }

            if (!binding.TryResolveVisibility(arguments, out bool visible))
            {
                Complete(false, $"'{binding.CommandName}' expects On or Off as its first argument.");
                return;
            }

            if (!_client.MiniTools.TryGetTool(binding.MiniToolId, out var descriptor))
            {
                Complete(false, $"The connected Player does not provide mini-tool '{binding.MiniToolId}'.");
                return;
            }

            ApplyPresentation(binding, descriptor, visible);

            if (binding.Routing ==
                RemoteCommandRouting.ExecuteInBuildAndControlEditorTool)
            {
                _client.Commands.Execute(commandLine);
                return;
            }

            string action = visible ? "Started" : "Stopped";
            string displayName = string.IsNullOrWhiteSpace(descriptor.DisplayName) ? binding.MiniToolId : descriptor.DisplayName;
            Complete(true, $"{action} Editor presentation for '{displayName}'.");
        }

        private bool TryResolveBinding(
            string commandName,
            out RemoteCommandPresentationBinding binding)
        {
            if (RemoteCommandPresentationRegistry
                    .TryGetAdvancedRegistration(
                        commandName,
                        out binding))
                return true;

            if (RemoteCommandPresentationRegistry.TryGetProjectOverride(
                    commandName,
                    out binding))
                return true;

            if (RemoteMiniToolCommandManifestResolver.TryFindBinding(
                    _client.MiniTools.Tools,
                    commandName,
                    out RemoteCommandPresentationBinding targetBinding) &&
                !RemoteCommandPresentationRegistry
                    .HasProjectOverrideForMiniTool(
                        targetBinding.MiniToolId))
            {
                binding = targetBinding;
                return true;
            }

            return RemoteCommandPresentationRegistry.TryGetDefinitionBinding(
                commandName,
                out binding);
        }

        private void ApplyPresentation(RemoteCommandPresentationBinding binding, RemoteMiniToolDescriptor descriptor, bool visible)
        {
            RemoteMiniToolVisibilitySettings settings = RemoteMiniToolVisibilitySettings.instance;
            settings.RegisterCatalog(_client.MiniTools.Tools);
            if (visible)
                settings.SetVisible(binding.MiniToolId, true);

            _client.MiniTools.SetSubscription(binding.MiniToolId, visible, descriptor.DefaultIntervalSeconds);
        }

        private void Complete(bool success, string message)
        {
            _client.Commands.CompleteLocally(new RemoteCommandExecuteResponse
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

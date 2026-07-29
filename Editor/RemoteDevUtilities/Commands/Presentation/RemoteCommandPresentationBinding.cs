using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation
{
    /// <summary>
    /// Converts command arguments into the desired visibility of an Editor mini-tool.
    /// </summary>
    public delegate bool RemoteCommandVisibilityParser(IReadOnlyList<string> arguments, out bool visible);

    /// <summary>
    /// Maps an existing console command to an Editor-side remote mini-tool presentation.
    /// </summary>
    public sealed class RemoteCommandPresentationBinding
    {
        private readonly RemoteCommandVisibilityParser _visibilityParser;

        public RemoteCommandPresentationBinding(
            string commandName,
            string miniToolId,
            RemoteCommandRouting routing =
                RemoteCommandRouting.ControlEditorToolOnly,
            RemoteCommandVisibilityParser visibilityParser = null)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("A command name is required.", nameof(commandName));
            if (commandName.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
            {
                throw new ArgumentException("The command name cannot contain whitespace.", nameof(commandName));
            }
            if (!Enum.IsDefined(typeof(RemoteCommandRouting), routing))
                throw new ArgumentOutOfRangeException(nameof(routing));
            if (routing != RemoteCommandRouting.ExecuteInBuildOnly &&
                string.IsNullOrWhiteSpace(miniToolId))
                throw new ArgumentException("A mini-tool id is required.", nameof(miniToolId));

            CommandName = commandName.Trim();
            MiniToolId = miniToolId?.Trim() ?? string.Empty;
            Routing = routing;
            _visibilityParser = visibilityParser ?? TryParseToggle;
        }

        public string CommandName { get; }
        public string MiniToolId { get; }
        public RemoteCommandRouting Routing { get; }

        public bool TryResolveVisibility(IReadOnlyList<string> arguments, out bool visible)
        {
            return _visibilityParser(arguments ?? Array.Empty<string>(), out visible);
        }

        /// <summary>
        /// Default parser used by a binding. No argument means show; the first argument accepts
        /// on/off, true/false, or 1/0. Additional arguments are left for the command.
        /// </summary>
        public static bool TryParseToggle(IReadOnlyList<string> arguments, out bool visible)
        {
            visible = true;
            if (arguments == null || arguments.Count == 0)
                return true;

            string value = arguments[0];
            if (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                visible = true;
                return true;
            }

            if (value.Equals("off", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                visible = false;
                return true;
            }

            return false;
        }
    }
}

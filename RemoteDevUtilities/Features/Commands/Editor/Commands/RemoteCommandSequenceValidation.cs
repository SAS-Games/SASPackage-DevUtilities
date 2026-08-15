using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.Sequences
{
    internal enum RemoteCommandSequenceStepAvailability
    {
        Ready,
        Disabled,
        Unknown,
        Empty,
        MissingCommand
    }

    internal readonly struct RemoteCommandSequenceStepValidation
    {
        internal RemoteCommandSequenceStepValidation(RemoteCommandSequenceStepAvailability availability, string message)
        {
            Availability = availability;
            Message = message ?? string.Empty;
        }

        internal RemoteCommandSequenceStepAvailability Availability { get; }
        internal string Message { get; }
        internal bool BlocksExecution => Availability == RemoteCommandSequenceStepAvailability.Empty;
    }

    internal static class RemoteCommandSequenceValidator
    {
        internal static RemoteCommandSequenceStepValidation Validate(
            RemoteCommandSequenceStep step,
            bool connected,
            string prefix,
            RemoteCommandDescriptor[] commands)
        {
            if (step == null || !step.Enabled)
                return new RemoteCommandSequenceStepValidation(RemoteCommandSequenceStepAvailability.Disabled, "Disabled");

            string commandLine = step.CommandLine?.Trim() ?? string.Empty;
            if (!RemoteCommandPresentationCoordinator.TryParseCommandLine(
                    commandLine, prefix, out string commandName, out _))
            {
                return new RemoteCommandSequenceStepValidation(
                    RemoteCommandSequenceStepAvailability.Empty, "Enter a command");
            }

            if (!connected)
            {
                return new RemoteCommandSequenceStepValidation(
                    RemoteCommandSequenceStepAvailability.Unknown, "Connect to validate");
            }

            commands ??= Array.Empty<RemoteCommandDescriptor>();
            for (int i = 0; i < commands.Length; i++)
            {
                string catalogName = commands[i]?.Name;
                if (string.IsNullOrWhiteSpace(catalogName))
                    continue;
                if (string.Equals(catalogName, commandName, StringComparison.OrdinalIgnoreCase) ||
                    commandName.StartsWith(catalogName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return new RemoteCommandSequenceStepValidation(
                        RemoteCommandSequenceStepAvailability.Ready, "Ready");
                }
            }

            return new RemoteCommandSequenceStepValidation(
                RemoteCommandSequenceStepAvailability.MissingCommand,
                step.WhenUnavailable == RemoteCommandUnavailablePolicy.WaitUntilAvailable
                    ? $"Wait up to {step.WaitTimeoutSeconds:0.##}s for '{commandName}'"
                    : $"'{commandName}' will fail if still unavailable");
        }
    }
}

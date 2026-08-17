using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Commands;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.Logging.Settings
{
    internal enum RemoteLoggingLevel
    {
        Info,
        Warning,
        Error
    }

    internal enum RemoteStackTraceTarget
    {
        All,
        Log,
        Warning,
        Error,
        Exception
    }

    internal static class RemoteLoggingCommandBuilder
    {
        internal const string ClearTagsCommand = "Logging.ClearTags";
        private const string LoggingCommandName = "Logging";

        internal static string SetLogLevel(RemoteLoggingLevel level, bool enabled)
        {
            return $"{LoggingCommandName}.LogLevel {level} {(enabled ? "On" : "Off")}";
        }

        internal static string SetStackTrace(RemoteStackTraceTarget target, StackTraceLogType mode)
        {
            int value = mode switch
            {
                StackTraceLogType.None => 0,
                StackTraceLogType.ScriptOnly => 1,
                StackTraceLogType.Full => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            return $"{LoggingCommandName}.SetStackTrace {target} {value}";
        }

        internal static bool TrySetTags(IEnumerable<string> tags, out string command, out string[] normalizedTags, out string error)
        {
            var uniqueTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var values = new List<string>();

            if (tags != null)
            {
                foreach (string tag in tags)
                {
                    string normalized = tag?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(normalized))
                        continue;

                    if (normalized.IndexOf('|') >= 0 || ContainsWhitespace(normalized))
                    {
                        command = null;
                        normalizedTags = Array.Empty<string>();
                        error = $"Tag '{normalized}' is invalid. Tags cannot contain whitespace or '|'.";
                        return false;
                    }

                    if (uniqueTags.Add(normalized))
                        values.Add(normalized);
                }
            }

            normalizedTags = values.ToArray();
            command = values.Count == 0 ? ClearTagsCommand : $"{LoggingCommandName}.SetTags {string.Join("|", values)}";
            error = null;
            return true;
        }

        internal static bool IsAvailable(IRemoteCommandExecutor commands) =>
            commands?.HasCommand(LoggingCommandName) == true;

        private static bool ContainsWhitespace(string value)
        {
            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character))
                    return true;
            }

            return false;
        }
    }
}

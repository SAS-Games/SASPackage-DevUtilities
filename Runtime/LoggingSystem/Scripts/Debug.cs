using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SAS
{
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Info = 1 << 0,
        Warning = 1 << 1,
        Error = 1 << 2,
    }

    public static partial class Debug
    {
        const string DEBUG = "DEBUG";
        private static LogLevel LogLevel = (LogLevel)(7);
        private static HashSet<string> AllowedTags = new HashSet<string>();

        public static void SetLogLevel(int level)
        {
            LogLevel = (LogLevel)level;
            Debug.Log($"Updated LogLevel: {LogLevel}");
        }

        public static void SetLogLevel(LogLevel level, bool isOn)
        {
            var newLevel = LogLevel;

            if (isOn)
                newLevel |= level; // Set bit
            else
                newLevel &= ~level; // Clear bit

            SetLogLevel((int)newLevel);
        }

        public static bool IsLogLevelEnabled(LogLevel levelToCheck)
        {
            return (LogLevel & levelToCheck) != 0;
        }

        public static void SetAllowedTags(IEnumerable<string> tags)
        {
            AllowedTags.Clear();
            AllowedTags.UnionWith(tags);
        }

        [Conditional(DEBUG)]
        public static void Log(object message, string tag = null)
        {
            Log(message?.ToString() ?? "null", tag, LogLevel.Info);
        }

        [Conditional(DEBUG)]
        public static void Log(string message, string tag = null)
        {
            Log(message, tag, LogLevel.Info);
        }

        [Conditional(DEBUG)]
        private static void Log(string message, string tag, LogLevel level)
        {
            if (CanLog(level) && TagPassesFilter(tag))
            {
                string logMessage = $"Tag: [{tag}] {message}";
                if (level == LogLevel.Info)
                    UnityEngine.Debug.Log(logMessage);
                else if (level == LogLevel.Warning)
                    UnityEngine.Debug.LogWarning(logMessage);
                else if (level == LogLevel.Error)
                    UnityEngine.Debug.LogError(logMessage);

                AddOnScreenLogEntry(message, tag, level);
            }
        }

        [Conditional(DEBUG)]
        public static void LogWarning(object message, string tag = null)
        {
            LogWarning(message?.ToString() ?? "null", tag);
        }

        [Conditional(DEBUG)]
        public static void LogWarning(string message, string tag = null)
        {
            Log(message, tag, LogLevel.Warning);
        }

        [Conditional(DEBUG)]
        public static void LogError(object message, string tag = null)
        {
            LogError(message?.ToString() ?? "null", tag);
        }

        [Conditional(DEBUG)]
        public static void LogError(string message, string tag = null)
        {
            Log(message, tag, LogLevel.Error);
        }

        public static void LogException(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            AddOnScreenLogEntry(exception.ToString(), "EXCEPTION", LogLevel.Error);
        }

        public static bool CanLog(LogLevel level)
        {
            return LogLevel.HasFlag(level);
        }

        private static bool TagPassesFilter(string tag)
        {
            // If no allowed tags or the tag is present in the allowed tags, the filter passes
            return AllowedTags.Count == 0 || AllowedTags.Contains(tag);
        }

        public static void DrawRay(Vector3 rayOrigin, Vector3 vector3, Color color)
        {
            UnityEngine.Debug.DrawRay(rayOrigin, vector3, color);
        }

        // Partial method implemented in the other file
        private static partial void AddOnScreenLogEntry(string message, string tag, LogLevel level);
    }
}
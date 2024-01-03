using System;
using System.Collections.Generic;

namespace SAS
{
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Info = 1,
        Warning = 1 << 1,
        Error = 1 << 2,
    }

    public static class Debug
    {
        private static LogLevel _logLevel = LogLevel.None;
        private static LogLevel mLogMask = LogLevel.None;
        private static HashSet<string> mAllowedTags = new HashSet<string>();

        public static void SetLogLevel(LogLevel level)
        {
            _logLevel = level;
        }

        public static void AddLogMask(LogLevel mask)
        {
            mLogMask |= mask;
        }

        public static void SetAllowedTags(IEnumerable<string> tags)
        {
            mAllowedTags.Clear();
            mAllowedTags.UnionWith(tags);
        }

        public static void ClearLogMask()
        {
            mLogMask = LogLevel.None;
        }

        public static void Log(string message, string tag = null, LogLevel level = LogLevel.Info)
        {
            if (CanLog(level) && TagPassesFilter(tag))
            {
                string logMessage = $"{level}: {message}";
                UnityEngine.Debug.Log(logMessage);
            }
        }

        public static void LogWarning(string message, string tag = null)
        {
            Log(message, tag, LogLevel.Warning);
        }

        public static void LogError(string message, string tag = null)
        {
            Log(message, tag, LogLevel.Error);
        }

        public static void LogException(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }

        public static bool CanLog(LogLevel level)
        {
            if ((_logLevel & level) != 0 && (mLogMask & level) != 0)
                return true;
            else
                return false;
        }

        private static bool TagPassesFilter(string tag)
        {
            // If no allowed tags or the tag is present in the allowed tags, the filter passes
            return mAllowedTags.Count == 0 || mAllowedTags.Contains(tag);
        }
    }
}

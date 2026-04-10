using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SAS
{
    public class LogHandler : ILogHandler
    {
        private ILogHandler m_DefaultLogHandler = UnityEngine.Debug.unityLogger.logHandler;

        public LogHandler()
        {
            UnityEngine.Debug.unityLogger.logHandler = this;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
#if ENABLE_DEBUG
            LogLevel level = LogLevel.Info;
            int slotIndex = -1;
            string tag = string.Empty;
            if (args.Length > 1)
            {
                if (!int.TryParse(args[1] as string, out slotIndex))
                    slotIndex = -1;

                if (args.Length > 2)
                    tag = args[2] as string;
            }
            string contextObjName = context?.name;
            if (Debug.CanLog(level) && Debug.TagPassesFilter(tag, contextObjName))
            {
                m_DefaultLogHandler.LogFormat(logType, context, format, args);
                Debug.AddOnScreenLogEntry(args[0] as string, tag, level, slotIndex);
            }
#endif
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            m_DefaultLogHandler.LogException(exception, context);
        }
    }

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
        const string DEBUG = "ENABLE_DEBUG";
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

        public static void ClearTags()
        {
            AllowedTags.Clear();
        }

        [Conditional(DEBUG)]
        public static void Log(object message, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message?.ToString() ?? "null", null, tag, LogLevel.Info, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void Log(string message, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, LogLevel.Info, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void Log(string message, int slotIndex, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, LogLevel.Info, slotIndex, caller);
        }

        [Conditional(DEBUG)]
        private static void Log(string message, string tag, LogLevel level, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, level, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void Log(object message, UnityEngine.Object context, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message?.ToString() ?? "null", context, tag, LogLevel.Info, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void Log(string message, UnityEngine.Object context, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, context, tag, LogLevel.Info, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void Log(string message, UnityEngine.Object context, int slotIndex, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, context, tag, LogLevel.Info, slotIndex, caller);
        }


        [Conditional(DEBUG)]
        public static void LogWarning(object message, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message?.ToString() ?? "null", null, tag, LogLevel.Warning, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogWarning(object message, UnityEngine.Object context, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message?.ToString() ?? "null", context, tag, LogLevel.Warning, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogWarning(string message, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, LogLevel.Warning, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogWarning(string message, int slotIndex, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, LogLevel.Warning, slotIndex, caller);
        }

        [Conditional(DEBUG)]
        public static void LogWarning(string message, UnityEngine.Object context, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, context, tag, LogLevel.Warning, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogWarning(string message, UnityEngine.Object context, int slotIndex, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, context, tag, LogLevel.Warning, slotIndex, caller);
        }

        [Conditional(DEBUG)]
        public static void LogError(object message, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message?.ToString() ?? "null", null, tag, LogLevel.Error, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogError(object message, UnityEngine.Object context, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message?.ToString() ?? "null", context, tag, LogLevel.Error, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogError(string message, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, LogLevel.Error, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogError(string message, int slotIndex, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, null, tag, LogLevel.Error, slotIndex, caller);
        }

        [Conditional(DEBUG)]
        public static void LogError(string message, UnityEngine.Object context, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, context, tag, LogLevel.Error, -1, caller);
        }

        [Conditional(DEBUG)]
        public static void LogError(string message, UnityEngine.Object context, int slotIndex, string tag = null, [CallerFilePath] string caller = "")
        {
            LogInternal(message, context, tag, LogLevel.Error, slotIndex, caller);
        }

        public static void LogException(Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            AddOnScreenLogEntry(exception.ToString(), "EXCEPTION", LogLevel.Error);
        }

        [Conditional(DEBUG)]
        private static void LogInternal(string message, UnityEngine.Object context, string tag, LogLevel level, int slotIndex, string callerFilePath = "")
        {
            if (string.IsNullOrEmpty(tag))
                tag = System.IO.Path.GetFileNameWithoutExtension(callerFilePath);

            string logMessage = $"Tag: [{tag}] {message}";
            if (level == LogLevel.Info)
                UnityEngine.Debug.LogFormat(LogType.Log, LogOption.None, context, "{0}", logMessage, slotIndex, tag);
            else if (level == LogLevel.Warning)
                UnityEngine.Debug.LogFormat(LogType.Warning, LogOption.None, context, "{0}", logMessage, slotIndex, tag);
            else if (level == LogLevel.Error)
                UnityEngine.Debug.LogFormat(LogType.Error, LogOption.None, context, "{0}", logMessage, slotIndex, tag);
        }

        public static bool CanLog(LogLevel level)
        {
            return LogLevel.HasFlag(level);
        }

        public static bool TagPassesFilter(string tag, string contextObjectName)
        {
            // If no allowed tags or the tag is present in the allowed tags, the filter passes
            return AllowedTags.Count == 0 || AllowedTags.Contains(tag) || AllowedTags.Contains(contextObjectName);
        }

        public static void DrawRay(Vector3 rayOrigin, Vector3 vector3, Color color)
        {
            UnityEngine.Debug.DrawRay(rayOrigin, vector3, color);
        }

        // Partial method implemented in the other file
        internal static partial void AddOnScreenLogEntry(string message, string tag, LogLevel level, int slotIndex = -1);
    }
}
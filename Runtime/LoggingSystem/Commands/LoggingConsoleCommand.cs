using SAS;
using SAS.Utilities.DeveloperConsole;
using System;
using System.Linq;
using UnityEngine;
using Debug = SAS.Debug;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = "HP/DeveloperConsole/Commands/Logging Console Command")]
    public class LoggingConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText;
        [SerializeField] private GameObject m_OnScreenLogPrefab;
        public override string HelpText => m_HelpText;
        private GameObject _onScreenLog;

        protected override void CommandMethodRegistry()
        {
            Register("LogLevel", LogLevel);
            Register("OnScreenLog", ShowOnScreen);
            Register("SetTags", SetTags);
            Register("ClearTags", ClearTags);
            Register("SetLogLifetime", SetLogLifetime);
            Register("ClearOnScreenLog", ClearOnScreenLog);
            Register("SetStackTrace", SetStackTrace);
        }

        private bool LogLevel(string[] args)
        {
            if (args.Length < 2)
                return false;

            LogLevel logLevel = SAS.LogLevel.None;
            if (args.Contains(SAS.LogLevel.Info.ToString()))
                logLevel = SAS.LogLevel.Info;
            if (args.Contains(SAS.LogLevel.Warning.ToString()))
                logLevel = SAS.LogLevel.Warning;
            if (args.Contains(SAS.LogLevel.Error.ToString()))
                logLevel = SAS.LogLevel.Error;

            bool enable = args[1].Equals("On", StringComparison.OrdinalIgnoreCase);
            Debug.SetLogLevel(logLevel, enable);

            return true;
        }

        private bool ShowOnScreen(string[] args)
        {
            if (args.Length < 1)
                return false;

            if (args[0].Equals("On", StringComparison.OrdinalIgnoreCase))
            {
                if (_onScreenLog == null)
                {
                    _onScreenLog = Instantiate(m_OnScreenLogPrefab);
                    _onScreenLog.name = "OnScreenLog";
                }
                _onScreenLog.SetActive(true);
            }
            else if (args[0].Equals("Off", StringComparison.OrdinalIgnoreCase))
            {
                if (_onScreenLog != null)
                    _onScreenLog.SetActive(false);
            }
            else
                return false;

            return true;
        }

        private bool SetTags(string[] args)
        {
            if (args.Length < 1)
                return false;

            var tags = args[0].Split('|')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToArray();

            if (tags.Length == 0)
                return false;

            Debug.SetAllowedTags(tags);
            return true;
        }

        private bool ClearTags(string[] args)
        {
            if (args.Length > 0)
                return false;

            Debug.ClearTags();
            return true;
        }

        private bool SetLogLifetime(string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float val))
                return false;

            if (_onScreenLog == null)
                return false;

            _onScreenLog.GetComponent<OnScreenLogUI>().SetLifetime(val);
            return true;
        }

        private bool ClearOnScreenLog(string[] args)
        {
            if (_onScreenLog == null)
                return false;

            _onScreenLog.GetComponent<OnScreenLogUI>().ClearLogs();
            return true;
        }

        private bool SetStackTrace(string[] args)
        {
            if (args.Length < 2)
            {
                Debug.LogWarning("Usage: SetStackTrace <Log|Warning|Error|Exception|All> <0=None, 1=ScriptOnly, 2=Full>");
                return false;
            }

            string logTypeArg = args[0];
            if (!int.TryParse(args[1], out int mode))
            {
                Debug.LogWarning("Invalid value. Use 0=None, 1=ScriptOnly, 2=Full");
                return false;
            }

            // Convert int to StackTraceLogType
            StackTraceLogType stackTraceType = mode switch
            {
                0 => StackTraceLogType.None,
                1 => StackTraceLogType.ScriptOnly,
                2 => StackTraceLogType.Full,
                _ => StackTraceLogType.ScriptOnly
            };

            if (logTypeArg.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                Application.SetStackTraceLogType(LogType.Log, stackTraceType);
                Application.SetStackTraceLogType(LogType.Warning, stackTraceType);
                Application.SetStackTraceLogType(LogType.Error, stackTraceType);
                Application.SetStackTraceLogType(LogType.Exception, stackTraceType);

                Debug.Log($"Stack trace for ALL log types set to {stackTraceType}");
            }
            else
            {
                if (!Enum.TryParse(logTypeArg, true, out LogType logType))
                    return false;

                Application.SetStackTraceLogType(logType, stackTraceType);
                Debug.Log($"Stack trace for {logType} set to {stackTraceType}");
            }

            return true;
        }
    }
}

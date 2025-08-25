using SAS;
using SAS.Utilities.DeveloperConsole;
using System;
using System.Linq;
using UnityEngine;
using Debug = SAS.Debug;

[CreateAssetMenu(menuName = "SAS/DeveloperConsole/Commands/Logging Console Command")]
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
        Register("SetLogLifetime", SetLogLifetime);
        Register("ClearOnScreenLog", ClearOnScreenLog);
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
                _onScreenLog = Instantiate(m_OnScreenLogPrefab);
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
}

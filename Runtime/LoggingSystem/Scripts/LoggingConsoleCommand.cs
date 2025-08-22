using System;
using System.Linq;
using SAS;
using SAS.Utilities.DeveloperConsole;
using UnityEngine;
using Debug = SAS.Debug;

[CreateAssetMenu(menuName = "SAS/Utilities/DeveloperConsole/Commands/Logging Console Command")]
public class LoggingConsoleCommand : CompositeConsoleCommand
{
    [SerializeField] private string m_HelpText;
    [SerializeField] private GameObject m_OnScreenLogPrefab;
    public override string HelpText => m_HelpText;

    private GameObject _onScreenLog;

    public void LogLevel(string[] args, CommandResult result)
    {
        if (args.Length < 2)
        {
            result.Success = false;
            return;
        }

        LogLevel logLevel = SAS.LogLevel.None;
        if (args.Contains(SAS.LogLevel.Info.ToString()))
            logLevel = SAS.LogLevel.Info;
        if (args.Contains(SAS.LogLevel.Warning.ToString()))
            logLevel = SAS.LogLevel.Warning;
        if (args.Contains(SAS.LogLevel.Error.ToString()))
            logLevel = SAS.LogLevel.Error;
        Debug.SetLogLevel(logLevel, args[1].Equals("On", StringComparison.OrdinalIgnoreCase) ? true : false);
        result.Success = true;
    }

    public void ShowOnScreen(string[] args, CommandResult result)
    {
        if (args.Length < 1)
        {
            result.Success = false;
            return;
        }

        if (args[0].Equals("On", StringComparison.OrdinalIgnoreCase))
        {
            if (_onScreenLog == null)
                _onScreenLog = Instantiate(m_OnScreenLogPrefab);
            _onScreenLog.SetActive(true);
        }
        else if (args[0].Equals("Off", StringComparison.OrdinalIgnoreCase))
            _onScreenLog.SetActive(false);

        result.Success = true;
    }

    public void SetTags(string[] args, CommandResult result)
    {
        if (args.Length < 1)
        {
            result.Success = false;
            return;
        }

        var tags = args[0].Split('|')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToArray();

        if (tags.Length == 0)
        {
            result.Success = false;
            return;
        }
        Debug.SetAllowedTags(tags);
        result.Success = true;
    }

    public void SetLogLifetime(string[] args, CommandResult result)
    {
        if (args.Length < 1)
        {
            result.Success = false;
            return;
        }

        if (!float.TryParse(args[0], out float val))
        {
            result.Success = false;
            return;
        }
        if (_onScreenLog == null)
        {
            result.Success = false;
            return;
        }
        _onScreenLog.GetComponent<OnScreenLogUI>().SetLifetime(val);
        result.Success = true;
    }

    public void ClearOnScreenLog(string[] args, CommandResult result)
    {
        if (_onScreenLog == null)
        {
            result.Success = false;
            return;
        }
        _onScreenLog.GetComponent<OnScreenLogUI>().ClearLogs();
        result.Success = true;
    }
}
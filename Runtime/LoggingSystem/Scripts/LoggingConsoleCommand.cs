using SAS;
using SAS.Utilities.DeveloperConsole;
using System;
using System.Linq;
using UnityEngine;
using Debug = SAS.Debug;

[CreateAssetMenu(menuName = "SAS/Utilities/DeveloperConsole/Commands/Logging Console Command")]
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

    private CommandResult LogLevel(string[] args)
    {
        if (args.Length < 2)
            return CommandResult.Fail();

        LogLevel logLevel = SAS.LogLevel.None;
        if (args.Contains(SAS.LogLevel.Info.ToString()))
            logLevel = SAS.LogLevel.Info;
        if (args.Contains(SAS.LogLevel.Warning.ToString()))
            logLevel = SAS.LogLevel.Warning;
        if (args.Contains(SAS.LogLevel.Error.ToString()))
            logLevel = SAS.LogLevel.Error;

        bool enable = args[1].Equals("On", StringComparison.OrdinalIgnoreCase);
        Debug.SetLogLevel(logLevel, enable);

        return CommandResult.Ok();
    }

    private CommandResult ShowOnScreen(string[] args)
    {
        if (args.Length < 1)
            return CommandResult.Fail();

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
            return CommandResult.Fail();

        return CommandResult.Ok();
    }

    private CommandResult SetTags(string[] args)
    {
        if (args.Length < 1)
            return CommandResult.Fail();

        var tags = args[0].Split('|')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToArray();

        if (tags.Length == 0)
            return CommandResult.Fail();

        Debug.SetAllowedTags(tags);
        return CommandResult.Ok();
    }

    private CommandResult SetLogLifetime(string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out float val))
            return CommandResult.Fail();

        if (_onScreenLog == null)
            return CommandResult.Fail();

        _onScreenLog.GetComponent<OnScreenLogUI>().SetLifetime(val);
        return CommandResult.Ok();
    }

    private CommandResult ClearOnScreenLog(string[] args)
    {
        if (_onScreenLog == null)
            return CommandResult.Fail();

        _onScreenLog.GetComponent<OnScreenLogUI>().ClearLogs();
        return CommandResult.Ok();
    }
}

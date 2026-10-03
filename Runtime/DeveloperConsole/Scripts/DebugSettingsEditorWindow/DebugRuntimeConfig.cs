using System.Collections.Generic;
using SAS;
using SAS.Utilities.DeveloperConsole;
using UnityEngine;

public class DebugRuntimeConfig : ScriptableObject
{
    private static DebugRuntimeConfig s_BuildSnapshot;

    public bool pauseOnEnable;
    public DeveloperConsoleInputSettings consoleInput = new();
    public List<string> hiddenConsoleCommands = new();
    public LogLevel logLevel;
    public List<string> allowedTags;

    [SerializeField, HideInInspector] private bool m_IsBuildSnapshot;

    internal bool IsBuildSnapshot => m_IsBuildSnapshot;

    internal void Apply(bool pause, LogLevel level, IEnumerable<string> tags, bool isBuildSnapshot)
    {
        Apply(pause, level, tags, null, null, isBuildSnapshot);
    }

    internal void Apply(bool pause, LogLevel level, IEnumerable<string> tags,
        DeveloperConsoleInputSettings inputSettings, bool isBuildSnapshot)
    {
        Apply(pause, level, tags, inputSettings, null, isBuildSnapshot);
    }

    internal void Apply(bool pause, LogLevel level, IEnumerable<string> tags,
        DeveloperConsoleInputSettings inputSettings, IEnumerable<string> hiddenCommands,
        bool isBuildSnapshot)
    {
        pauseOnEnable = pause;
        consoleInput ??= new DeveloperConsoleInputSettings();
        if (inputSettings == null)
            consoleInput.ResetToDefaults();
        else
            consoleInput.CopyFrom(inputSettings);
        hiddenConsoleCommands = hiddenCommands == null
            ? new List<string>()
            : new List<string>(hiddenCommands);
        logLevel = level;
        allowedTags = tags == null ? new List<string>() : new List<string>(tags);
        m_IsBuildSnapshot = isBuildSnapshot;

        if (m_IsBuildSnapshot)
            s_BuildSnapshot = this;
        else if (ReferenceEquals(s_BuildSnapshot, this))
            s_BuildSnapshot = null;
    }

    internal static DebugRuntimeConfig LoadOrCreateDefaults()
    {
        if (s_BuildSnapshot != null)
            return s_BuildSnapshot;

        foreach (DebugRuntimeConfig candidate in Resources.FindObjectsOfTypeAll<DebugRuntimeConfig>())
        {
            if (candidate != null && candidate.m_IsBuildSnapshot)
            {
                s_BuildSnapshot = candidate;
                return candidate;
            }
        }

        DebugRuntimeConfig defaults = CreateInstance<DebugRuntimeConfig>();
        defaults.hideFlags = HideFlags.HideAndDontSave;
        defaults.Apply(false, LogLevel.Info | LogLevel.Warning | LogLevel.Error, null, false);
        return defaults;
    }

    private void OnEnable()
    {
        consoleInput ??= new DeveloperConsoleInputSettings();
        hiddenConsoleCommands ??= new List<string>();
        allowedTags ??= new List<string>();
        if (m_IsBuildSnapshot)
            s_BuildSnapshot = this;
    }

    private void OnDisable()
    {
        if (ReferenceEquals(s_BuildSnapshot, this))
            s_BuildSnapshot = null;
    }
}

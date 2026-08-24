using System.Collections.Generic;
using SAS;
using UnityEngine;

public class DebugRuntimeConfig : ScriptableObject
{
    private static DebugRuntimeConfig s_BuildSnapshot;

    public bool pauseOnEnable;
    public LogLevel logLevel;
    public List<string> allowedTags;

    [SerializeField, HideInInspector] private bool m_IsBuildSnapshot;

    internal bool IsBuildSnapshot => m_IsBuildSnapshot;

    internal void Apply(bool pause, LogLevel level, IEnumerable<string> tags, bool isBuildSnapshot)
    {
        pauseOnEnable = pause;
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

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using SAS.Utilities.DeveloperConsole.Editor;
#endif
using UnityEngine;


namespace SAS.Utilities.DeveloperConsole
{
    public static class DebugSettings
    {
        public static bool PauseOnEnable { get; private set; }
        public static DeveloperConsoleInputSettings ConsoleInput { get; private set; } = new();
        public static LogLevel LogLevel { get; private set; }
        public static IReadOnlyList<string> AllowedTags => _allowedTags;
        public static IReadOnlyCollection<string> HiddenConsoleCommands => _hiddenConsoleCommands;
        public static event Action ConsoleCommandVisibilityChanged;

        private static List<string> _allowedTags = new();
        private static HashSet<string> _hiddenConsoleCommands = new(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_EDITOR
            LoadFromEditorSettings();
#else
            LoadFromRuntimeAsset();
#endif
            Apply();
        }

#if UNITY_EDITOR
        private static void LoadFromEditorSettings()
        {
            var settings = DebugEditorSettings.instance;

            PauseOnEnable = settings.pauseOnEnable;
            ConsoleInput = settings.consoleInput?.Clone() ?? new DeveloperConsoleInputSettings();
            LogLevel = settings.logLevel;
            _allowedTags = settings.allowedTags == null
                ? new List<string>()
                : new List<string>(settings.allowedTags);
            SetHiddenConsoleCommands(settings.hiddenConsoleCommands);
        }
#endif

        private static void LoadFromRuntimeAsset()
        {
            DebugRuntimeConfig config = DebugRuntimeConfig.LoadOrCreateDefaults();

            PauseOnEnable = config.pauseOnEnable;
            ConsoleInput = config.consoleInput?.Clone() ?? new DeveloperConsoleInputSettings();
            LogLevel = config.logLevel;
            _allowedTags = config.allowedTags == null
                ? new List<string>()
                : new List<string>(config.allowedTags);
            SetHiddenConsoleCommands(config.hiddenConsoleCommands);
        }

        public static bool IsConsoleCommandVisible(string commandName)
        {
            return string.IsNullOrWhiteSpace(commandName) ||
                   !_hiddenConsoleCommands.Contains(commandName);
        }

        private static void SetHiddenConsoleCommands(IEnumerable<string> commandNames)
        {
            _hiddenConsoleCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (commandNames == null)
                return;

            foreach (string commandName in commandNames)
            {
                if (!string.IsNullOrWhiteSpace(commandName))
                    _hiddenConsoleCommands.Add(commandName.Trim());
            }
        }

        private static void Apply()
        {
            Debug.SetLogLevel((int)LogLevel);
            Debug.SetAllowedTags(_allowedTags);
            ConsoleCommandVisibilityChanged?.Invoke();
        }

#if UNITY_EDITOR
        public static void ApplyFromEditor()
        {
            LoadFromEditorSettings();
            Apply();
        }
#endif
    }
}

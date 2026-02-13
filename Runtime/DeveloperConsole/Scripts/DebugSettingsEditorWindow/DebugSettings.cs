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
        public static LogLevel LogLevel { get; private set; }
        public static IReadOnlyList<string> AllowedTags => _allowedTags;

        private static List<string> _allowedTags = new();

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
            LogLevel = settings.logLevel;
            _allowedTags = settings.allowedTags ?? new List<string>();
        }
#endif

        private static void LoadFromRuntimeAsset()
        {
            var config = Resources.Load<DebugRuntimeConfig>("DebugRuntimeConfig");

            if (config == null)
            {
                PauseOnEnable = false;
                LogLevel = LogLevel.Info | LogLevel.Warning | LogLevel.Error;
                _allowedTags = new List<string>();
                return;
            }

            PauseOnEnable = config.pauseOnEnable;
            LogLevel = config.logLevel;
            _allowedTags = config.allowedTags ?? new List<string>();
        }

        private static void Apply()
        {
            Debug.SetLogLevel((int)LogLevel);
            Debug.SetAllowedTags(_allowedTags);
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
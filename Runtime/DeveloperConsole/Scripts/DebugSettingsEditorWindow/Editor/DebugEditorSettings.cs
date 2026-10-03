#if UNITY_EDITOR
using System.Collections.Generic;
using SAS.Utilities.DeveloperConsole;
using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    [FilePath("ProjectSettings/DevUtilitiesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class DebugEditorSettings : ScriptableSingleton<DebugEditorSettings>
    {
        public bool pauseOnEnable = false;
        public DeveloperConsoleInputSettings consoleInput = new();
        public List<string> hiddenConsoleCommands = new();
        public LogLevel logLevel = LogLevel.Info | LogLevel.Warning | LogLevel.Error;
        public List<string> allowedTags = new();

        public void SaveSettings()
        {
            Save(true);
        }

        private void OnEnable()
        {
            consoleInput ??= new DeveloperConsoleInputSettings();
            hiddenConsoleCommands ??= new List<string>();
            allowedTags ??= new List<string>();
        }
    }
}
#endif

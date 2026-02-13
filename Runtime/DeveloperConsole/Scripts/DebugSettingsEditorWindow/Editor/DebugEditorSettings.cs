using UnityEditor;
using System.Collections.Generic;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public class DebugEditorSettings : ScriptableSingleton<DebugEditorSettings>
    {
        public bool pauseOnEnable = false;
        public LogLevel logLevel = LogLevel.Info | LogLevel.Warning | LogLevel.Error;
        public List<string> allowedTags = new();

        public void SaveSettings()
        {
            Save(true);
        }
    }
}
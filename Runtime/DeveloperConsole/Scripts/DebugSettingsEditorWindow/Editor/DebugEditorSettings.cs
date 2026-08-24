#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    [FilePath("ProjectSettings/DevUtilitiesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
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
#endif

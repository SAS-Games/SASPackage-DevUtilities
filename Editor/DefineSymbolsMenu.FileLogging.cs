using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public static partial class DefineSymbolsMenu
    {
        [MenuItem("Tools/DevUtilities/Defines/LOG_TO_FILE")]
        private static void Toggle_LOG_TO_FILE() => ToggleDefine("LOG_TO_FILE");

        [MenuItem("Tools/DevUtilities/Defines/LOG_TO_FILE", true)]
        private static bool Validate_LOG_TO_FILE() => ValidateToggle("LOG_TO_FILE");
    }
}
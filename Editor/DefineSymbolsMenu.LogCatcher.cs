using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public static partial class DefineSymbolsMenu
    {
        [MenuItem("Tools/DevUtilities/Defines/ENABLE_LOG_CATCHER")]
        private static void Toggle_ENABLE_LOG_CATCHER() => ToggleDefine("ENABLE_LOG_CATCHER");

        [MenuItem("Tools/DevUtilities/Defines/ENABLE_LOG_CATCHER", true)]
        private static bool Validate_ENABLE_LOG_CATCHER() => ValidateToggle("ENABLE_LOG_CATCHER");
    }
}
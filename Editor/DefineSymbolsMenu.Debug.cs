using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public static partial class DefineSymbolsMenu
    {
        [MenuItem("Tools/Defines/ENABLE_DEBUG")]
        private static void Toggle_ENABLE_DEBUG() => ToggleDefine("ENABLE_DEBUG");

        [MenuItem("Tools/Defines/ENABLE_DEBUG", true)]
        private static bool Validate_ENABLE_DEBUG() => ValidateToggle("ENABLE_DEBUG");
    }
}
using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public static partial class DefineSymbolsMenu
    {
        [MenuItem("Tools/Dev Utilities/Defines/STATE_MACHINE_DEBUG")]
        private static void Toggle_STATE_MACHINE_DEBUG() => ToggleDefine("STATE_MACHINE_DEBUG");

        [MenuItem("Tools/Dev Utilities/Defines/STATE_MACHINE_DEBUG", true)]
        private static bool Validate_STATE_MACHINE_DEBUG() => ValidateToggle("STATE_MACHINE_DEBUG");
    }
}

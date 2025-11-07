using UnityEditor;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public static partial class DefineSymbolsMenu
    {
        [MenuItem("Tools/Defines/UNITY_RENDER_PIPELINE_UNIVERSAL")]
        private static void Toggle_UNITY_RENDER_PIPELINE_UNIVERSAL() => ToggleDefine("UNITY_RENDER_PIPELINE_UNIVERSAL");

        [MenuItem("Tools/Defines/UNITY_RENDER_PIPELINE_UNIVERSAL", true)]
        private static bool Validate_UNITY_RENDER_PIPELINE_UNIVERSAL() => ValidateToggle("UNITY_RENDER_PIPELINE_UNIVERSAL");
    }
}
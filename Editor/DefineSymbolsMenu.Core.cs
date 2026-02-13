using System;
using UnityEditor;
using UnityEditor.Build;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public static partial class DefineSymbolsMenu
    {
        private static void ToggleDefine(string symbol)
        {
            bool has = HasSymbol(symbol);
            ModifyDefineSymbols(symbol, !has);
        }

        private static bool ValidateToggle(string symbol)
        {
            Menu.SetChecked($"Tools/DevUtilities/Defines/{symbol}", HasSymbol(symbol));
            return true;
        }

        private static void ModifyDefineSymbols(string symbol, bool add)
        {
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));

            var defineList = defines.Split(';');
            if (add && !HasSymbol(symbol, defineList))
                defines = string.IsNullOrEmpty(defines) ? symbol : defines + ";" + symbol;
            else if (!add && HasSymbol(symbol, defineList))
                defines = string.Join(";", Array.FindAll(defineList, d => d != symbol));

            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), defines);
            Debug.Log($"{(add ? "Added" : "Removed")} define symbol: {symbol}");
        }

        private static bool HasSymbol(string symbol, string[] defineList = null)
        {
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (defineList == null)
                defineList = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup)).Split(';');

            foreach (var d in defineList)
            {
                if (d == symbol)
                    return true;
            }

            return false;
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal static class RemoteInspectorFormatting
    {
        public static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "<unknown>";

            int separator = typeName.LastIndexOf('.');
            return separator >= 0 ? typeName.Substring(separator + 1) : typeName;
        }

        public static void SetExpanded<T>(HashSet<T> set, T value, bool expanded)
        {
            if (expanded)
                set.Add(value);
            else
                set.Remove(value);
        }
    }

    internal static class RemoteInspectorInput
    {
        internal static bool IsApplyKey(Event current, string focusedControlName, string expectedControlName)
        {
            return current != null && current.type == EventType.KeyDown &&
                   (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) &&
                   string.Equals(focusedControlName, expectedControlName, StringComparison.Ordinal);
        }
    }
}

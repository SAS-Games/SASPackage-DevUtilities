using System;
using System.Collections.Generic;

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
}

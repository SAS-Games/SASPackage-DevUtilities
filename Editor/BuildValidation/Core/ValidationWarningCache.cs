using System.Collections.Generic;

namespace HP.BuildValidation
{
    public static class ValidationWarningCache
    {
        public static readonly List<string> Warnings = new();

        public static void Clear()
        {
            Warnings.Clear();
        }
    }
}
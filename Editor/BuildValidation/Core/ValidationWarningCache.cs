using System.Collections.Generic;

namespace SAS.BuildValidation
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

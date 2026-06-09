using System;

namespace SAS.BuildValidation
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildValidationAttribute : Attribute
    {
        public bool Optional { get; }
        public int Order { get; }

        /// <summary>
        /// Validation can only run when a BuildReport is available.
        /// </summary>
        public bool RequiresBuildReport { get; }

        public BuildValidationAttribute(bool optional = true, int order = 0, bool requiresBuildReport = false)
        {
            Optional = optional;
            Order = order;
            RequiresBuildReport = requiresBuildReport;
        }
    }
}
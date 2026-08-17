using System;

namespace HP.BuildValidation
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildValidationAttribute : Attribute
    {
        /// <summary>
        /// Indicates whether this validation is optional.
        /// If true, the validation can be skipped or disabled.
        /// If false, the validation is mandatory and must always run.
        /// </summary>
        public bool Optional { get; }

        /// <summary>
        /// Determines the execution order of this validation relative to others.
        /// Validations with lower order values run before those with higher values.
        /// </summary>
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
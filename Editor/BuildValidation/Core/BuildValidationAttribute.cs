using System;

namespace SAS.BuildValidation
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildValidationAttribute : Attribute
    {
        public bool Optional { get; }
        public int Order { get; }

        public BuildValidationAttribute(bool optional = true, int order = 0)
        {
            Optional = optional;
            Order = order;
        }
    }
}
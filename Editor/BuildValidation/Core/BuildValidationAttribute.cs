using System;

namespace SAS.BuildValidation
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BuildValidationAttribute : Attribute
    {
        public int Order { get; private set; }

        public BuildValidationAttribute(int order = 0)
        {
            Order = order;
        }
    }
}
using System;

namespace SAS.BuildValidation
{
    [Serializable]
    public class ValidationState
    {
        public string TypeName;
        public bool Enabled = true;
    }
}
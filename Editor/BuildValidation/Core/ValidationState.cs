using System;

namespace HP.BuildValidation
{
    [Serializable]
    public class ValidationState
    {
        public string TypeName;
        public bool Enabled = true;
    }
}
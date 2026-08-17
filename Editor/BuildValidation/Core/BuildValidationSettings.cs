using System.Collections.Generic;
using UnityEngine;

namespace HP.BuildValidation
{
    public class BuildValidationSettings : ScriptableObject
    {
        public List<ValidationState> Validations = new List<ValidationState>();
    }
}
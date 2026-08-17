using UnityEngine;

namespace HP.BuildValidation
{
    public class ValidationIssue
    {
        public string Message;
        public ValidationSeverity Severity;
        public Object Context;
    }
}
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SAS.BuildValidation
{
    public class BuildValidationResult
    {
        public List<ValidationIssue> Issues { get; } = new();
        public bool HasErrors => Issues.Any(x => x.Severity == ValidationSeverity.Error);
        public bool HasWarnings => Issues.Any(x => x.Severity == ValidationSeverity.Warning);

        public static BuildValidationResult Create()
        {
            return new BuildValidationResult();
        }

        public void AddIssue(string message, ValidationSeverity severity = ValidationSeverity.Error, Object context = null)
        {
            Issues.Add(new ValidationIssue
            {
                Message = message,
                Severity = severity,
                Context = context
            });
        }
    }
}

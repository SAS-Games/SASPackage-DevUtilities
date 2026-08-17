using System;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HP.BuildValidation
{
    public class ValidationReport
    {
        public readonly List<string> Warnings = new();
        public readonly List<string> Errors = new();

        public bool HasWarnings => Warnings.Count > 0;
        public bool HasErrors => Errors.Count > 0;
    }
    
    public static class BuildValidationRunner
    {
        public static ValidationReport Run(BuildReport report = null)
        {
            ValidationReport validationReport = new();

            ValidationWarningCache.Clear();

            foreach (var type in BuildValidationRegistry.GetValidationTypes())
            {
                if (!BuildValidationUtility.IsValidationEnabled(type))
                    continue;

                if (report == null && BuildValidationUtility.RequiresBuildReport(type))
                    continue;
                
                if (Activator.CreateInstance(type) is not IBuildValidation validation)
                    continue;

                BuildValidationResult result = validation.Validate(report);

                foreach (var issue in result.Issues)
                {
                    string message = $"[{validation.Name}] {issue.Message}";

                    LogIssue(issue, message);

                    switch (issue.Severity)
                    {
                        case ValidationSeverity.Warning:
                            validationReport.Warnings.Add(message);
                            ValidationWarningCache.Warnings.Add(message);
                            break;

                        case ValidationSeverity.Error:
                            validationReport.Errors.Add(message);
                            break;
                    }
                }
            }

            return validationReport;
        }
        
        private static void LogIssue(ValidationIssue issue, string message)
        {
            switch (issue.Severity)
            {
                case ValidationSeverity.Warning:
                    Debug.LogWarning(message, issue.Context);
                    break;

                case ValidationSeverity.Error:
                    Debug.LogError(message, issue.Context);
                    break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SAS.BuildValidation
{
    public class BuildValidationRunner : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var validationTypes = BuildValidationRegistry.GetValidationTypes();

            List<string> warnings = new();
            List<string> errors = new();
            ValidationWarningCache.Clear();
            
            foreach (var type in validationTypes)
            {
                if (!BuildValidationUtility.IsValidationEnabled(type))
                    continue;

                if (Activator.CreateInstance(type) is not IBuildValidation validation)
                    continue;

                BuildValidationResult result = validation.Validate(report);

                foreach (var issue in result.Issues)
                {
                    string message = $"[{validation.Name}] " + $"{issue.Message}";

                    switch (issue.Severity)
                    {
                        case ValidationSeverity.Warning:
                            warnings.Add(message);
                            ValidationWarningCache.Warnings.Add(message);
                            break;

                        case ValidationSeverity.Error:
                            errors.Add(message);
                            break;
                    }

                    LogIssue(issue, message);
                }
            }

            if (warnings.Count == 0 && errors.Count == 0)
                return;

            string dialogMessage = BuildDialogMessage(warnings, errors);

            // CI / Batch Mode
            if (Application.isBatchMode)
            {
                if (errors.Count > 0)
                    throw new BuildFailedException(dialogMessage);
                return;
            }

            // No errors → only warnings
            if (errors.Count == 0)
            {
                Debug.Log(dialogMessage);
                return;
            }

            bool continueBuild = EditorUtility.DisplayDialog("Build Validation Failed", dialogMessage, "Continue Build", "Cancel Build");

            if (!continueBuild)
                throw new BuildFailedException("Build cancelled due to validation failure.");
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

        private static string BuildDialogMessage(List<string> warnings, List<string> errors)
        {
            System.Text.StringBuilder builder = new();
            
            if (errors.Count > 0)
            {
                builder.AppendLine("ERRORS");
                builder.AppendLine("--------------------");

                foreach (var error in errors)
                {
                    builder.AppendLine(error);
                }
            }
            
            if (warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("WARNINGS");
                builder.AppendLine("--------------------");

                foreach (var warning in warnings)
                {
                    builder.AppendLine(warning);
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
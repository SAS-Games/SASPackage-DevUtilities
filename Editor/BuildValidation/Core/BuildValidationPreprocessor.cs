using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HP.BuildValidation
{
    public class BuildValidationPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidationReport validationReport = BuildValidationRunner.Run(report);

            HandleBuildValidation(validationReport);
        }

        private static void HandleBuildValidation(ValidationReport report)
        {
            string dialogMessage = BuildValidationFormatter.Format(report);

            // CI / Batch Mode
            if (Application.isBatchMode)
            {
                if (report.HasErrors)
                    throw new BuildFailedException(dialogMessage);

                return;
            }

            // Only warnings
            if (!report.HasErrors)
            {
                Debug.Log(dialogMessage);
                return;
            }

            bool continueBuild = EditorUtility.DisplayDialog("Build Validation Failed", dialogMessage, "Continue Build", "Cancel Build");

            if (!continueBuild)
                throw new BuildFailedException("Build cancelled due to validation failure.");
        }
    }
}

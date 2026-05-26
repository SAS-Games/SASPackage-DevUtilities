using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SAS.BuildValidation
{
    public class BuildValidationPostProcessor
        : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (Application.isBatchMode)
                return;

            if (ValidationWarningCache.Warnings.Count == 0)
                return;

            StringBuilder builder = new();

            builder.AppendLine("Build completed with warnings.");
            builder.AppendLine();

            builder.AppendLine("WARNINGS");
            builder.AppendLine("--------------------");

            foreach (var warning in ValidationWarningCache.Warnings)
            {
                builder.AppendLine(warning);
            }

            EditorUtility.DisplayDialog("Build Validation Warnings", builder.ToString(), "OK");
            ValidationWarningCache.Clear();
        }
    }
}
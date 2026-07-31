using SAS.BuildValidation;
using UnityEditor;

public static class ValidationMenu
{
    [MenuItem("Tools/Build Validation/Run Validation")]
    public static void RunValidation()
    {
        ValidationReport report = BuildValidationRunner.Run();

        string message = BuildValidationFormatter.Format(report);

        if (!report.HasErrors && !report.HasWarnings)
        {
            EditorUtility.DisplayDialog("Validation", "All validations passed.", "OK");
            return;
        }

        EditorUtility.DisplayDialog("Validation Results", message, "OK");
    }
}

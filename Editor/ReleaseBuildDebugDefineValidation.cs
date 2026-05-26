using UnityEditor.Build.Reporting;

namespace SAS.BuildValidation
{
    [BuildValidation(optional: false)]
    public class ReleaseBuildDebugDefineValidation : IBuildValidation
    {
        public string Name => "Release Build Debug Define Validation";

        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result = BuildValidationResult.Create();

#if ENABLE_DEBUG

            bool isDevelopmentBuild = (report.summary.options.HasFlag( UnityEditor.BuildOptions.Development));

            if (!isDevelopmentBuild)
                result.AddIssue("DevUtility is enabled for a non-development build. " + "Disable debug defines before creating a release build.", ValidationSeverity.Error);
#endif

            return result;
        }
    }
}
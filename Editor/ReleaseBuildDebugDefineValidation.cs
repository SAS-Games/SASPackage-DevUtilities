using UnityEditor.Build.Reporting;

namespace SAS.BuildValidation
{
    [BuildValidation(order: 0)]
    public class ReleaseBuildDebugDefineValidation : IBuildValidation
    {
        public string Name => "Release Build Debug Define Validation";

        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result = BuildValidationResult.Create();

#if ENABLE_DEBUG

            bool isDevelopmentBuild = (report.summary.options & UnityEditor.BuildOptions.Development) != 0;

            if (!isDevelopmentBuild)
                result.AddIssue("DevUtility is enabled for a non-development build. " + "Disable debug defines before creating a release build.", ValidationSeverity.Error);
#endif

            return result;
        }
    }
}
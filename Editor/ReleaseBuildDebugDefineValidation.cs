using UnityEditor.Build.Reporting;

namespace SAS.BuildValidation
{
    [BuildValidation(optional: false, requiresBuildReport: true)]
    public class ReleaseBuildDebugDefineValidation : IBuildValidation
    {
        public string Name => "Release Build Debug Define Validation";

        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result = BuildValidationResult.Create();

#if ENABLE_DEBUG

            bool isDevelopmentBuild = (report.summary.options.HasFlag(UnityEditor.BuildOptions.Development));

            if (!isDevelopmentBuild)
                result.AddIssue(
                    "ENABLE_DEBUG is enabled for a non-Development build. The complete Dev Utilities " +
                    "runtime and its direct TCP debugging transport will be included. Remove the " +
                    "define before distributing a public production build.",
                    ValidationSeverity.Warning);
#endif

            return result;
        }
    }
}

#if ENABLE_DEBUG
using HP.BuildValidation;
using UnityEditor.Build.Reporting;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    /// <summary>
    /// Reports unified mini-tool registration issues through the package's
    /// shared build-validation pipeline.
    /// </summary>
    [BuildValidation(optional: false, order: -100)]
    public sealed class MiniToolRegistrationBuildValidation :
        IBuildValidation
    {
        public string Name => "Mini-Tool Registration";

        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result =
                BuildValidationResult.Create();

            foreach (string error in MiniToolRegistry.ValidationErrors)
            {
                result.AddIssue(
                    error,
                    ValidationSeverity.Error);
            }

            foreach (string warning in MiniToolRegistry.ValidationWarnings)
            {
                result.AddIssue(
                    warning,
                    ValidationSeverity.Warning);
            }

            return result;
        }
    }
}
#endif

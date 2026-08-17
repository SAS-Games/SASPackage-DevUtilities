using UnityEditor.Build.Reporting;

namespace HP.BuildValidation
{
    public interface IBuildValidation
    {
        string Name { get; }
        BuildValidationResult Validate(BuildReport report);
    }
}
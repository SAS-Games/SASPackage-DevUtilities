using UnityEditor.Build.Reporting;

namespace SAS.BuildValidation
{
    public interface IBuildValidation
    {
        string Name { get; }
        BuildValidationResult Validate(BuildReport report);
    }
}

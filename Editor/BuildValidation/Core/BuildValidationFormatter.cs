using System.Text;

namespace SAS.BuildValidation
{
    public static class BuildValidationFormatter
    {
        public static string Format(ValidationReport report)
        {
            StringBuilder builder = new();

            if (report.Errors.Count > 0)
            {
                builder.AppendLine("ERRORS");
                builder.AppendLine("--------------------");

                foreach (var error in report.Errors)
                {
                    builder.AppendLine(error);
                }
            }

            if (report.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("WARNINGS");
                builder.AppendLine("--------------------");

                foreach (var warning in report.Warnings)
                {
                    builder.AppendLine(warning);
                }
            }

            return builder.ToString();
        }
    }
}
using System;

namespace SAS.BuildValidation
{
    public static class BuildValidationUtility
    {
        public static bool IsValidationEnabled(Type type)
        {
            var settings = BuildValidationSettingsProvider.GetOrCreateSettings();
            string typeName = type.FullName;

            var state = settings.Validations.Find(v => v.TypeName == typeName);

            if (state == null)
            {
                state = new ValidationState
                {
                    TypeName = typeName,
                    Enabled = true
                };

                settings.Validations.Add(state);
            }

            return state.Enabled;
        }
    }
}
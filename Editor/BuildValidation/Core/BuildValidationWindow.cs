using System;
using UnityEditor;
using UnityEngine;

namespace SAS.BuildValidation
{
    public class BuildValidationWindow : EditorWindow
    {
        [MenuItem("Tools/DevUtilities/Build Validation")]
        public static void Open()
        {
            GetWindow<BuildValidationWindow>("Build Validation");
        }

        private void OnGUI()
        {
            var settings = BuildValidationSettingsProvider.GetOrCreateSettings();
            var validationTypes = BuildValidationRegistry.GetValidationTypes();

            foreach (var type in validationTypes)
            {
                DrawValidation(type, settings);
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(settings);
            }
        }

        private void DrawValidation(Type type, BuildValidationSettings settings)
        {
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

            state.Enabled = EditorGUILayout.ToggleLeft(type.Name, state.Enabled);
        }
    }
}
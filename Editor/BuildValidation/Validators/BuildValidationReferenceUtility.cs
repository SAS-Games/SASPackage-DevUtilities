using UnityEditor;
using UnityEngine;

namespace SAS.BuildValidation
{
    internal static class BuildValidationReferenceUtility
    {
        public static void ValidateSerializedObject(SerializedObject serializedObject, string assetPath, Object context, BuildValidationResult result)
        {
            SerializedProperty iterator = serializedObject.GetIterator();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (!IsMissingReference(iterator))
                    continue;

                string message = $"{assetPath}\n" + $"Object: {GetObjectName(context)}\n" + $"Component: {context.GetType().Name}\n" + $"Field: {iterator.displayName}";
                result.AddIssue(message, ValidationSeverity.Error, context);
            }
        }

        private static string GetObjectName(Object context)
        {
            if (context is Component component)
                return component.gameObject.name;

            return context.name;
        }

        private static bool IsMissingReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (property.objectReferenceValue != null)
                return false;

            return property.objectReferenceInstanceIDValue != 0;
        }
    }
}
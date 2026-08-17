using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HP.BuildValidation
{
    [BuildValidation(optional: false, order: 3)]
    public sealed class MissingReferencesValidation : IBuildValidation
    {
        public string Name => "Missing References";

        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result = BuildValidationResult.Create();

            ValidatePrefabs(result);
            ValidateScriptableObjects(result);
            return result;
        }

        private static void ValidatePrefabs(BuildValidationResult result)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = null;

                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    Component[] components = prefabRoot.GetComponentsInChildren<Component>(true);

                    foreach (Component component in components)
                    {
                        if (component == null)
                            continue;

                        ValidateSerializedObject(new SerializedObject(component), path, component, result);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (prefabRoot != null)
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void ValidateScriptableObjects(BuildValidationResult result)
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset == null)
                    continue;

                ValidateSerializedObject(new SerializedObject(asset), path, asset, result);
            }
        }

        private static void ValidateSerializedObject(SerializedObject serializedObject, string assetPath, Object context, BuildValidationResult result)
        {
            SerializedProperty iterator = serializedObject.GetIterator();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (!IsMissingReference(iterator))
                    continue;

                string componentName = context.GetType().Name;
                string message = $"{assetPath}\n" + $"Component: {componentName}\n" + $"Field: {iterator.displayName}";

                result.AddIssue(message, ValidationSeverity.Error, context);
            }
        }

        private static bool IsMissingReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            if (property.objectReferenceValue != null)
                return false;

            return property.objectReferenceInstanceIDValue != 0 && IsVisibleInInspector(property);
        }

        private static bool IsVisibleInInspector(SerializedProperty property)
        {
            if (!property.editable)
                return false;

            var field = property.serializedObject.targetObject.GetType().GetField(property.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
                return false;

            return field.GetCustomAttribute<HideInInspector>() == null;
        }
    }
}

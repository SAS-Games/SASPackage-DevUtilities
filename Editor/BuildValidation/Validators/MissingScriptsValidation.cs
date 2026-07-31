using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.BuildValidation
{
    [BuildValidation(optional: false, order: 1)]
    public class MissingScriptsValidation : SceneBuildValidationBase
    {
        public override string Name => "Missing Scripts Validation";

        protected override void ValidateScene(Scene scene, BuildValidationResult result)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                ValidateHierarchy(root.transform, scene.name, result);
            }
        }

        private void ValidateHierarchy(Transform current, string sceneName, BuildValidationResult result)
        {
            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject);
            if (missingScripts > 0)
                result.AddIssue($"{sceneName} -> " + $"{GetHierarchyPath(current)} " + $"contains {missingScripts} missing scripts.", ValidationSeverity.Error, current.gameObject);

            for (int i = 0; i < current.childCount; i++)
            {
                ValidateHierarchy(current.GetChild(i), sceneName, result);
            }
        }

        private string GetHierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = $"{current.name}/{path}";
            }

            return path;
        }
    }
}

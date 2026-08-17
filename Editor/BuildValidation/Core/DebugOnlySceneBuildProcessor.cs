using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HP.BuildValidation
{
    public sealed class DebugOnlySceneBuildProcessor : IProcessSceneWithReport
    {
        private const string DebugOnlyTag = "DebugOnly";

        public int callbackOrder => 0;

        private static bool IsDebugEnabled
        {
            get
            {
#if ENABLE_DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (IsDebugEnabled || report == null || (report.summary.options & BuildOptions.Development) != 0)
                return;

            int removedObjectCount = RemoveDebugOnlyObjects(scene);
            if (removedObjectCount > 0)
                Debug.Log($"[Build Validation] Removed {removedObjectCount} object(s) tagged '{DebugOnlyTag}' from '{scene.path}' for the non-Development build.");
        }

        private static int RemoveDebugOnlyObjects(Scene scene)
        {
            List<GameObject> objectsToRemove = new();

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (Transform transform in rootObject.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.gameObject.tag == DebugOnlyTag)
                        objectsToRemove.Add(transform.gameObject);
                }
            }

            objectsToRemove.Sort((left, right) => GetHierarchyDepth(right.transform).CompareTo(GetHierarchyDepth(left.transform)));

            foreach (GameObject objectToRemove in objectsToRemove)
                Object.DestroyImmediate(objectToRemove);

            return objectsToRemove.Count;
        }

        private static int GetHierarchyDepth(Transform transform)
        {
            int depth = 0;

            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }
    }
}

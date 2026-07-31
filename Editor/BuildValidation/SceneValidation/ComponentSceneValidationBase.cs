using UnityEngine;
using UnityEngine.SceneManagement;

namespace SAS.BuildValidation
{
    public abstract class ComponentSceneValidationBase<T> : SceneBuildValidationBase where T : Component
    {
        protected override void ValidateScene(Scene scene, BuildValidationResult result)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                T[] components = root.GetComponentsInChildren<T>(true);

                foreach (var component in components)
                {
                    ValidateComponent(component, scene, result);
                }
            }
        }

        protected abstract void ValidateComponent(T component, Scene scene, BuildValidationResult result);

        protected string GetHierarchyPath(Transform current)
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

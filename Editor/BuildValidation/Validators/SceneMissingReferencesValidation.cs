using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HP.BuildValidation
{
    [BuildValidation(optional: true, order: 2)]
    public sealed class SceneMissingReferencesValidation : IBuildValidation
    {
        public string Name => "Scene Missing References";

        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result = BuildValidationResult.Create();

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                ValidateScenes(result);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            return result;
        }

        private static void ValidateScenes(BuildValidationResult result)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;

                Scene openedScene = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Additive);

                try
                {
                    foreach (GameObject rootObject in openedScene.GetRootGameObjects())
                    {
                        Component[] components = rootObject.GetComponentsInChildren<Component>(true);

                        foreach (Component component in components)
                        {
                            if (component == null)
                                continue;

                            BuildValidationReferenceUtility.ValidateSerializedObject(new SerializedObject(component), scene.path, component, result);
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(openedScene, true);
                }
            }
        }
    }
}
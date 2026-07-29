using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SAS.BuildValidation
{
    public abstract class SceneBuildValidationBase : IBuildValidation
    {
        public abstract string Name { get; }
        
        public BuildValidationResult Validate(BuildReport report)
        {
            BuildValidationResult result = BuildValidationResult.Create();

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            bool hasLoadedScene = false;
            foreach (SceneSetup setup in originalSetup)
            {
                if (setup.isLoaded)
                {
                    hasLoadedScene = true;
                    break;
                }
            }

            try
            {
                foreach (var buildScene in EditorBuildSettings.scenes)
                {
                    if (!buildScene.enabled)
                        continue;

                    Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                    ValidateScene(scene, result);
                }
            }
            finally
            {
                if (hasLoadedScene)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        originalSetup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }

            return result;
        }

        protected abstract void ValidateScene(Scene scene, BuildValidationResult result);
    }
}

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
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            return result;
        }

        protected abstract void ValidateScene(Scene scene, BuildValidationResult result);
    }
}
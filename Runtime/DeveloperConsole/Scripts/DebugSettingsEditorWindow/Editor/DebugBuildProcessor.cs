using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    public class DebugBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
#if !ENABLE_DEBUG
            return;
#endif

            var editorSettings = DebugEditorSettings.instance;

            var runtimeConfig = ScriptableObject.CreateInstance<DebugRuntimeConfig>();
            runtimeConfig.pauseOnEnable = editorSettings.pauseOnEnable;
            runtimeConfig.logLevel = editorSettings.logLevel;
            runtimeConfig.allowedTags = new System.Collections.Generic.List<string>(editorSettings.allowedTags);

            const string folder = "Assets/Resources";

            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            const string assetPath = folder + "/DebugRuntimeConfig.asset";

            if (AssetDatabase.LoadAssetAtPath<DebugRuntimeConfig>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(runtimeConfig, assetPath);
            AssetDatabase.SaveAssets();
        }
    }
}
using System;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.DebugHost.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal static class RemoteDebugHostSceneLoader
    {
        private const string SceneAssetGuid =
            "15b437ea13444955a2f2e05c81eedf72";

        internal static string SceneAssetPath =>
            AssetDatabase.GUIDToAssetPath(SceneAssetGuid);

        internal static bool TryOpen(out string error)
        {
            error = string.Empty;
            string scenePath = SceneAssetPath;
            if (string.IsNullOrWhiteSpace(scenePath) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                error = "The Debug Host scene asset could not be resolved.";
                return false;
            }

            try
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (scene.IsValid() && scene.isLoaded && ValidateScene(scene, out error))
                {
                    return true;
                }

                if (string.IsNullOrEmpty(error))
                    error = $"Unity did not load the Debug Host scene at '{scenePath}'.";
                return false;
            }
            catch (Exception exception)
            {
                error = $"Debug Host scene '{scenePath}' could not be opened. " + exception.Message;
                return false;
            }
        }

        private static bool ValidateScene(Scene scene, out string error)
        {
            if (!Contains<RemoteDebugHostEnvironmentView>(scene))
            {
                error = "The Debug Host scene does not contain its environment view.";
                return false;
            }

            if (!Contains<Camera>(scene))
            {
                error = "The Debug Host scene does not contain a camera.";
                return false;
            }

            if (!Contains<EventSystem>(scene))
            {
                error = "The Debug Host scene does not contain an EventSystem.";
                return false;
            }

            if (!Contains<DeveloperConsoleBehaviour>(scene))
            {
                error = "The Debug Host scene does not contain the Developer Console prefab.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool Contains<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<T>(true) != null)
                    return true;
            }

            return false;
        }
    }
}

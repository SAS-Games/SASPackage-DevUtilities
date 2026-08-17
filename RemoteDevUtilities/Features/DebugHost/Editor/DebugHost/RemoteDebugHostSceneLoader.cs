using System;
using HP.Utilities.RemoteDevUtilities.DebugHost.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal static class RemoteDebugHostSceneLoader
    {
        private const string EnvironmentPrefabGuid = "37a03349eaf30bc4cafc546d68d1bef3";

        internal static bool TryCreate(out string error)
        {
            error = string.Empty;
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = AssetDatabase.GUIDToAssetPath(EnvironmentPrefabGuid);
                GameObject prefab = string.IsNullOrWhiteSpace(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    error = $"The Debug Host environment prefab could not be resolved from GUID " +
                            $"'{EnvironmentPrefabGuid}'.";
                    return false;
                }

                if (PrefabUtility.InstantiatePrefab(prefab, scene) is not GameObject)
                {
                    error = $"The Debug Host environment prefab at '{path}' could not be instantiated.";
                    return false;
                }

                return ValidateScene(scene, out error);
            }
            catch (Exception exception)
            {
                error = "The temporary Debug Host scene could not be created. " + exception.Message;
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

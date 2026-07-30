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
        private const string EnvironmentPrefabGuid =
            "37a03349eaf30bc4cafc546d68d1bef3";
        private const string ConsolePrefabGuid =
            "6590d4eca4ab3de42a2372b05d8cc2e2";

        internal static bool TryCreate(out string error)
        {
            error = string.Empty;
            try
            {
                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                if (!TryInstantiatePrefab(
                        EnvironmentPrefabGuid,
                        "Debug Host environment",
                        scene,
                        out error) ||
                    !TryInstantiatePrefab(
                        ConsolePrefabGuid,
                        "Developer Console",
                        scene,
                        out error))
                    return false;

                return ValidateScene(scene, out error);
            }
            catch (Exception exception)
            {
                error =
                    "The temporary Debug Host scene could not be created. " +
                    exception.Message;
                return false;
            }
        }

        private static bool TryInstantiatePrefab(
            string guid,
            string displayName,
            Scene scene,
            out string error)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                error =
                    $"{displayName} prefab could not be resolved from GUID " +
                    $"'{guid}'.";
                return false;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                error =
                    $"{displayName} prefab at '{path}' could not be " +
                    "instantiated.";
                return false;
            }

            error = string.Empty;
            return true;
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

using System;
using System.Collections.Generic;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    [InitializeOnLoad]
    internal static class RemoteDebugHostLauncher
    {
        private const string SessionKey = "RemoteDevUtilities.DebugHostRequested";
        private const string PreviousScenesKey = "RemoteDevUtilities.PreviousScenes";
        private const string RestoreScenesKey = "RemoteDevUtilities.RestoreScenes";
        private static bool _installed;
        private static IReadOnlyList<IRemoteDebugHostContribution> _contributions =
            Array.Empty<IRemoteDebugHostContribution>();

        [Serializable]
        private sealed class SceneSetupCollection
        {
            public SceneSetupRecord[] Scenes = Array.Empty<SceneSetupRecord>();
        }

        [Serializable]
        private sealed class SceneSetupRecord
        {
            public string Path;
            public bool IsLoaded;
            public bool IsActive;
        }

        static RemoteDebugHostLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal static bool IsActive => _installed || SessionState.GetBool(SessionKey, false);

        internal static void Launch()
        {
            SessionState.SetBool(SessionKey, true);
            UI.RemoteDevUtilitiesWindow.Open();
            if (EditorApplication.isPlaying)
            {
                SessionState.EraseBool(SessionKey);
                Debug.LogWarning("Exit the current Play Mode session before launching the isolated " + "Remote Dev Utilities Debug Host.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                SessionState.EraseBool(SessionKey);
                return;
            }

            SaveCurrentSceneSetup();
            if (!RemoteDebugHostSceneLoader.TryCreate(out string error))
            {
                Debug.LogWarning(error + " The Debug Host will continue with the available " + "temporary scene objects.");
            }

            EditorApplication.EnterPlaymode();
        }

        internal static void Stop()
        {
            SessionState.EraseBool(SessionKey);
            Uninstall();
            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(RestoreScenesKey, true);
                EditorApplication.ExitPlaymode();
            }
            else
            {
                RestorePreviousSceneSetup();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SessionKey, false))
                Install();
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (SessionState.GetBool(SessionKey, false))
                    SessionState.SetBool(RestoreScenesKey, true);
                SessionState.EraseBool(SessionKey);
                Uninstall();
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RestoreScenesKey, false))
            {
                RestorePreviousSceneSetup();
            }
        }

        private static void Install()
        {
            if (_installed)
                return;

            RemoteDevUtilitiesClient client = RemoteDevUtilitiesEditorService.Client;
            DeveloperConsoleBehaviour console = UnityEngine.Object.FindFirstObjectByType<DeveloperConsoleBehaviour>(FindObjectsInactive.Include);
            if (console == null)
            {
                GameObject prefab = Resources.Load<GameObject>("ConsoleCommandsSystem");
                if (prefab != null)
                    console = UnityEngine.Object.Instantiate(prefab).GetComponent<DeveloperConsoleBehaviour>();
            }

            _contributions = RemoteDebugHostContributionRegistry.CreateContributions();
            for (int i = 0; i < _contributions.Count; i++)
                _contributions[i].Install(client);
            _installed = true;
        }

        private static void Uninstall()
        {
            for (int i = _contributions.Count - 1; i >= 0; i--)
                _contributions[i].Uninstall();
            _contributions = Array.Empty<IRemoteDebugHostContribution>();
            _installed = false;
        }

        private static void SaveCurrentSceneSetup()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var records = new SceneSetupRecord[setup.Length];
            for (int i = 0; i < setup.Length; i++)
            {
                records[i] = new SceneSetupRecord
                {
                    Path = setup[i].path,
                    IsLoaded = setup[i].isLoaded,
                    IsActive = setup[i].isActive
                };
            }

            SessionState.SetString(PreviousScenesKey, JsonUtility.ToJson(new SceneSetupCollection { Scenes = records }));
        }

        private static void RestorePreviousSceneSetup()
        {
            SessionState.EraseBool(RestoreScenesKey);
            string json = SessionState.GetString(PreviousScenesKey, string.Empty);
            SessionState.EraseString(PreviousScenesKey);
            if (string.IsNullOrWhiteSpace(json))
                return;

            SceneSetupCollection collection = JsonUtility.FromJson<SceneSetupCollection>(json);
            var setup = new List<SceneSetup>();
            foreach (SceneSetupRecord record in collection?.Scenes ?? Array.Empty<SceneSetupRecord>())
            {
                if (string.IsNullOrWhiteSpace(record.Path))
                    continue;
                setup.Add(new SceneSetup
                {
                    path = record.Path,
                    isLoaded = record.IsLoaded,
                    isActive = record.IsActive
                });
            }

            if (setup.Count > 0)
                EditorSceneManager.RestoreSceneManagerSetup(setup.ToArray());
        }
    }
}

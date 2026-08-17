using System;
using System.Collections.Generic;
using HP.Utilities.DeveloperConsole;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    [InitializeOnLoad]
    internal static class RemoteDebugHostLauncher
    {
        private const string SessionKey = "RemoteDevUtilities.DebugHostRequested";
        private const string PreviousScenesKey = "RemoteDevUtilities.PreviousScenes";
        private const string RestoreScenesKey = "RemoteDevUtilities.RestoreScenes";
        private const string SuppressAutoLaunchKey = "RemoteDevUtilities.SuppressDebugHostAutoLaunch";
        private static bool _installed;
        private static bool _wasConnected;
        private static bool _autoLaunchScheduled;
        private static RemoteDevUtilitiesClient _observedClient;
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
            RemoteDebugHostSettings.Changed += OnSettingsChanged;
            if (!Application.isBatchMode &&
                RemoteDebugHostSettings.instance.LaunchDebugHostOnPlayerConnect)
                EditorApplication.delayCall += ObserveClient;
        }

        internal static bool IsActive => _installed || SessionState.GetBool(SessionKey, false);

        [InitializeOnEnterPlayMode]
        private static void ConfigureConsoleAutoSpawn(EnterPlayModeOptions options)
        {
            bool hostRequested = SessionState.GetBool(SessionKey, false);
            AutoSpawnConsoleCommandsSystem.SuppressAutomaticSpawn =
                ShouldSuppressConsoleAutoSpawn(
                    hostRequested,
                    RemoteDebugHostSettings.instance.IncludeDeveloperConsoleUi);
        }

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
            bool includeConsoleUi = RemoteDebugHostSettings.instance.IncludeDeveloperConsoleUi;
            AutoSpawnConsoleCommandsSystem.SuppressAutomaticSpawn =
                ShouldSuppressConsoleAutoSpawn(true, includeConsoleUi);
            if (!RemoteDebugHostSceneLoader.TryCreate(out string error))
            {
                SessionState.EraseBool(SessionKey);
                AutoSpawnConsoleCommandsSystem.SuppressAutomaticSpawn = false;
                RestorePreviousSceneSetup();
                Debug.LogError(error + " The Debug Host was not launched.");
                return;
            }

            EditorApplication.EnterPlaymode();
        }

        internal static void Stop()
        {
            SessionState.SetBool(SuppressAutoLaunchKey, true);
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
                {
                    SessionState.SetBool(SuppressAutoLaunchKey, true);
                    SessionState.SetBool(RestoreScenesKey, true);
                }
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
            if (RemoteDebugHostSettings.instance.IncludeDeveloperConsoleUi &&
                UnityEngine.Object.FindFirstObjectByType<DeveloperConsoleBehaviour>(FindObjectsInactive.Include) == null)
            {
                GameObject prefab = Resources.Load<GameObject>("ConsoleCommandsSystem");
                if (prefab != null)
                    UnityEngine.Object.Instantiate(prefab);
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

        private static void ObserveClient()
        {
            if (Application.isBatchMode ||
                !RemoteDebugHostSettings.instance.LaunchDebugHostOnPlayerConnect)
            {
                StopObservingClient();
                return;
            }

            RemoteDevUtilitiesClient client = RemoteDevUtilitiesEditorService.Client;
            if (ReferenceEquals(_observedClient, client))
                return;

            if (_observedClient != null)
                _observedClient.StateChanged -= OnClientStateChanged;
            _observedClient = client;
            _wasConnected = client.IsConnected;
            _observedClient.StateChanged += OnClientStateChanged;
        }

        private static void OnSettingsChanged()
        {
            if (!Application.isBatchMode &&
                RemoteDebugHostSettings.instance.LaunchDebugHostOnPlayerConnect)
                ObserveClient();
            else
                StopObservingClient();
        }

        private static void StopObservingClient()
        {
            if (_observedClient != null)
                _observedClient.StateChanged -= OnClientStateChanged;
            _observedClient = null;
            _wasConnected = false;
            _autoLaunchScheduled = false;
        }

        private static void OnClientStateChanged()
        {
            bool connected = _observedClient?.IsConnected == true;
            if (!connected)
            {
                _wasConnected = false;
                _autoLaunchScheduled = false;
                bool restoringHostScenes = SessionState.GetBool(RestoreScenesKey, false);
                if (!restoringHostScenes && !IsActive &&
                    !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.EraseBool(SuppressAutoLaunchKey);
                }
                return;
            }

            bool wasConnected = _wasConnected;
            _wasConnected = true;
            if (!ShouldAutoLaunch(
                    wasConnected,
                    connected,
                    RemoteDebugHostSettings.instance.LaunchDebugHostOnPlayerConnect,
                    IsActive,
                    EditorApplication.isPlayingOrWillChangePlaymode,
                    SessionState.GetBool(SuppressAutoLaunchKey, false)))
            {
                return;
            }

            _autoLaunchScheduled = true;
            EditorApplication.delayCall += TryAutoLaunch;
        }

        private static void TryAutoLaunch()
        {
            if (!_autoLaunchScheduled)
                return;

            _autoLaunchScheduled = false;
            if (Application.isBatchMode || _observedClient?.IsConnected != true ||
                !RemoteDebugHostSettings.instance.LaunchDebugHostOnPlayerConnect ||
                IsActive || EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool(SuppressAutoLaunchKey, false))
            {
                return;
            }

            Launch();
        }

        internal static bool ShouldAutoLaunch(
            bool wasConnected,
            bool connected,
            bool enabled,
            bool hostActive,
            bool editorPlayingOrChangingPlayMode,
            bool suppressed)
        {
            return !wasConnected && connected && enabled && !hostActive &&
                   !editorPlayingOrChangingPlayMode && !suppressed;
        }

        internal static bool ShouldSuppressConsoleAutoSpawn(
            bool hostRequested,
            bool includeDeveloperConsoleUi)
        {
            return hostRequested && !includeDeveloperConsoleUi;
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

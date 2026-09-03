using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Agent
{
    [InitializeOnLoad]
    internal static class EditorPlayModeDevUtilitiesAgentBootstrap
    {
        private static GameObject _agentObject;
        private static RemoteDevUtilitiesRuntimeSettings _runtimeSettings;

        static EditorPlayModeDevUtilitiesAgentBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyOwnedAgent;
            EditorApplication.quitting += DestroyOwnedAgent;
            EditorApplication.delayCall += SynchronizeWithPlayMode;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    SpawnAgent();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                case PlayModeStateChange.EnteredEditMode:
                    DestroyOwnedAgent();
                    break;
            }
        }

        private static void SynchronizeWithPlayMode()
        {
            if (EditorApplication.isPlaying)
                SpawnAgent();
            else
                DestroyOwnedAgent();
        }

        private static void SpawnAgent()
        {
            if (!EditorApplication.isPlaying || RuntimeDevUtilitiesAgent.Instance != null)
                return;

            RemoteDevUtilitiesProjectSettings projectSettings = RemoteDevUtilitiesProjectSettings.instance;
            if (!projectSettings.Runtime.EnableRemoteAgent)
                return;

            RemoteDevUtilitiesRuntimeSettings runtimeSettings = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            runtimeSettings.Apply(projectSettings.Runtime, false);
            runtimeSettings.name = "RemoteDevUtilitiesEditorPlayModeSettings";
            runtimeSettings.hideFlags = HideFlags.HideAndDontSave;

            var agentObject = new GameObject("[Remote Dev Utilities Agent]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            agentObject.SetActive(false);
            RuntimeDevUtilitiesAgent agent = agentObject.AddComponent<RuntimeDevUtilitiesAgent>();
            agent.Initialize(runtimeSettings, preserveLocalPresentation: true);

            _runtimeSettings = runtimeSettings;
            _agentObject = agentObject;
            agentObject.SetActive(true);
        }

        private static void DestroyOwnedAgent()
        {
            if (_agentObject != null)
                Object.DestroyImmediate(_agentObject);
            _agentObject = null;

            if (_runtimeSettings != null)
                Object.DestroyImmediate(_runtimeSettings);
            _runtimeSettings = null;
        }
    }
}

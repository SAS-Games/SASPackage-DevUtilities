using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Configuration
{
    [FilePath("ProjectSettings/RemoteDevUtilitiesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class RemoteDevUtilitiesProjectSettings : ScriptableSingleton<RemoteDevUtilitiesProjectSettings>
    {
        internal const string SettingsPath = "Project/Dev Utilities/Remote Dev Utilities";

        [SerializeField] private bool _runtimeConfigurationInitialized;
        [SerializeField] private RemoteDevUtilitiesRuntimeConfiguration _runtime = new();
        [SerializeField] private RemoteMiniToolVisibilityConfiguration _visibility = new();
        [SerializeField] private RemoteMiniToolPresentationConfiguration _presentations = new();
        [SerializeField] private RemoteMiniToolCommandConfiguration _commands = new();

        internal RemoteDevUtilitiesRuntimeConfiguration Runtime
        {
            get
            {
                EnsureRuntimeConfiguration();
                RuntimeSceneInspectorSettings.ConfigureDefaults(_runtime.RuntimeSceneInspector);
                return _runtime;
            }
        }

        internal RemoteMiniToolVisibilityConfiguration Visibility => _visibility ??= new RemoteMiniToolVisibilityConfiguration();
        internal RemoteMiniToolPresentationConfiguration Presentations => _presentations ??= new RemoteMiniToolPresentationConfiguration();
        internal RemoteMiniToolCommandConfiguration Commands => _commands ??= new RemoteMiniToolCommandConfiguration();

        internal void Persist()
        {
            Save(true);
        }

        internal static void Open()
        {
            _ = instance.Runtime;
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        [InitializeOnLoadMethod]
        private static void ConfigureEditorRuntimeSceneInspector()
        {
            _ = instance.Runtime;
        }

        private void EnsureRuntimeConfiguration()
        {
            if (_runtimeConfigurationInitialized)
                return;

            _runtime ??= new RemoteDevUtilitiesRuntimeConfiguration();
            RemoteDevUtilitiesRuntimeSettings packageDefaults = Resources.Load<RemoteDevUtilitiesRuntimeSettings>("RemoteDevUtilitiesSettings");
            if (packageDefaults != null)
                _runtime.CopyFrom(packageDefaults);

            _runtimeConfigurationInitialized = true;
            Persist();
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Remote Dev Utilities",
                guiHandler = _ => DrawSettings(),
                keywords = new HashSet<string>
                {
                    "Remote",
                    "Debug",
                    "TCP",
                    "Port",
                    "LAN Discovery",
                    "Diagnostic Logs",
                    "Access Token",
                    "Build UI",
                    "Mini Tools",
                    "Scene Inspector",
                    "Frame Recorder",
                    "Experimental"
                }
            };
            return provider;
        }

        private static void DrawSettings()
        {
            RemoteDevUtilitiesProjectSettings settings = instance;
            _ = settings.Runtime;

            EditorGUILayout.HelpBox(
                "Runtime settings are baked into ENABLE_DEBUG Players. Editor mini-tool settings are stored in the same ProjectSettings file.",
                MessageType.Info);

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();
            SerializedProperty runtime = serializedSettings.FindProperty("_runtime");
            bool frameRecorderWasEnabled = settings.Runtime.EnableExperimentalFrameRecorder;
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(
                (EditorGUIUtility.currentViewWidth - 40f) * 0.4f, 240f, 320f);
            bool changed;
            try
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(runtime, new GUIContent("Runtime Build Settings"), true);
                changed = EditorGUI.EndChangeCheck();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            RemoteDevUtilitiesRuntimeConfiguration configuration = settings.Runtime;
            if (configuration.EnableLanDiscovery &&
                (!configuration.AllowTcpConnectionsFromOtherMachines ||
                 string.IsNullOrWhiteSpace(configuration.TcpAccessToken)))
            {
                EditorGUILayout.HelpBox(
                    "LAN discovery is enabled but will not run in the Player. Enable TCP connections from other " +
                    "machines and set a non-empty TCP access token.",
                    MessageType.Warning);
            }

            if (!changed)
                return;

            serializedSettings.ApplyModifiedProperties();
            RuntimeSceneInspectorSettings.ConfigureDefaults(settings.Runtime.RuntimeSceneInspector);
            settings.Persist();
            if (frameRecorderWasEnabled != settings.Runtime.EnableExperimentalFrameRecorder)
                EditorApplication.delayCall += EditorUtility.RequestScriptReload;
        }
    }
}

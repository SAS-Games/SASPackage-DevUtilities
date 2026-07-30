using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Configuration
{
    [FilePath(
        "ProjectSettings/RemoteDevUtilitiesSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class RemoteDevUtilitiesProjectSettings :
        ScriptableSingleton<RemoteDevUtilitiesProjectSettings>
    {
        internal const string SettingsPath =
            "Project/Dev Utilities/Remote Dev Utilities";

        [SerializeField]
        private bool _runtimeConfigurationInitialized;

        [SerializeField]
        private RemoteDevUtilitiesRuntimeConfiguration
            _runtime = new();

        [SerializeField]
        private RemoteMiniToolVisibilityConfiguration _visibility = new();

        [SerializeField]
        private RemoteMiniToolPresentationConfiguration _presentations = new();

        [SerializeField]
        private RemoteMiniToolCommandConfiguration _commands = new();

        internal RemoteDevUtilitiesRuntimeConfiguration Runtime
        {
            get
            {
                EnsureRuntimeConfiguration();
                return _runtime;
            }
        }

        internal RemoteMiniToolVisibilityConfiguration Visibility =>
            _visibility ??= new RemoteMiniToolVisibilityConfiguration();

        internal RemoteMiniToolPresentationConfiguration Presentations =>
            _presentations ??=
                new RemoteMiniToolPresentationConfiguration();

        internal RemoteMiniToolCommandConfiguration Commands =>
            _commands ??= new RemoteMiniToolCommandConfiguration();

        internal void Persist()
        {
            Save(true);
        }

        internal static void Open()
        {
            _ = instance.Runtime;
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        private void EnsureRuntimeConfiguration()
        {
            if (_runtimeConfigurationInitialized)
                return;

            _runtime ??= new RemoteDevUtilitiesRuntimeConfiguration();
            RemoteDevUtilitiesRuntimeSettings packageDefaults =
                Resources.Load<RemoteDevUtilitiesRuntimeSettings>(
                    "RemoteDevUtilitiesSettings");
            if (packageDefaults != null)
                _runtime.CopyFrom(packageDefaults);

            _runtimeConfigurationInitialized = true;
            Persist();
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider(
                SettingsPath,
                SettingsScope.Project)
            {
                label = "Remote Dev Utilities",
                guiHandler = _ => DrawSettings(),
                keywords = new HashSet<string>
                {
                    "Remote",
                    "Debug",
                    "TCP",
                    "Port",
                    "Access Token",
                    "Build UI",
                    "Mini Tools"
                }
            };
            return provider;
        }

        private static void DrawSettings()
        {
            RemoteDevUtilitiesProjectSettings settings = instance;
            _ = settings.Runtime;

            EditorGUILayout.HelpBox(
                "These project settings are baked into ENABLE_DEBUG Players. " +
                "Editor-only mini-tool configuration is stored in the same " +
                "ProjectSettings file.",
                MessageType.Info);

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();
            SerializedProperty runtime =
                serializedSettings.FindProperty("_runtime");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                runtime,
                new GUIContent("Runtime Build Settings"),
                true);
            if (!EditorGUI.EndChangeCheck())
                return;

            serializedSettings.ApplyModifiedProperties();
            settings.Persist();
        }
    }
}

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

        private static readonly GUIContent[] SettingsPages =
        {
            new("General"),
            new("Scene Inspector"),
            new("Network")
        };

        private static int s_SelectedPage;
        private static bool s_ShowAccessToken;

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
                "These settings are baked into Players built with ENABLE_DEBUG. Changes apply to the next build; Editor-only mini-tool preferences are stored separately in this ProjectSettings file.",
                MessageType.Info);

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();
            SerializedProperty runtime = serializedSettings.FindProperty("_runtime");
            bool frameRecorderWasEnabled = settings.Runtime.EnableExperimentalFrameRecorder;

            s_SelectedPage = GUILayout.Toolbar(s_SelectedPage, SettingsPages,
                EditorStyles.toolbarButton, GUILayout.MinHeight(24f));
            GUILayout.Space(6f);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(
                (EditorGUIUtility.currentViewWidth - 40f) * 0.36f, 170f, 280f);
            bool changed;
            try
            {
                EditorGUI.BeginChangeCheck();
                switch (s_SelectedPage)
                {
                    case 1:
                        DrawSceneInspectorSettings(runtime);
                        break;
                    case 2:
                        DrawNetworkSettings(runtime);
                        break;
                    default:
                        DrawGeneralSettings(runtime);
                        break;
                }
                changed = EditorGUI.EndChangeCheck();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            if (!changed)
                return;

            serializedSettings.ApplyModifiedProperties();
            RuntimeSceneInspectorSettings.ConfigureDefaults(settings.Runtime.RuntimeSceneInspector);
            settings.Persist();
            if (frameRecorderWasEnabled != settings.Runtime.EnableExperimentalFrameRecorder)
                EditorApplication.delayCall += EditorUtility.RequestScriptReload;
        }

        private static void DrawGeneralSettings(SerializedProperty runtime)
        {
            SerializedProperty enableAgent = Property(runtime, "_enableRemoteAgent");
            DrawSection("Runtime Agent",
                "Controls whether the Player starts the remote diagnostics agent and how its in-game UI behaves.",
                () =>
                {
                    EditorGUILayout.PropertyField(enableAgent, new GUIContent("Enable Remote Agent"));
                    using (new EditorGUI.DisabledScope(!enableAgent.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(runtime, "_buildDebugUiVisibility"),
                            new GUIContent("Debug UI in Build"));
                        EditorGUILayout.PropertyField(Property(runtime, "_keepPlayerRunningInBackground"),
                            new GUIContent("Keep Player Running in Background"));
                    }
                });

            DrawSection("Capabilities",
                "Choose which remote tools are compiled into and exposed by ENABLE_DEBUG Players.",
                () =>
                {
                    using (new EditorGUI.DisabledScope(!enableAgent.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(runtime, "_allowCommandExecution"),
                            new GUIContent("Command Execution"));
                        EditorGUILayout.PropertyField(Property(runtime, "_streamLogs"),
                            new GUIContent("Log Streaming"));
                        EditorGUILayout.PropertyField(Property(runtime, "_allowMiniTools"),
                            new GUIContent("Mini Tools"));
                    }
                });

            SerializedProperty frameRecorder = Property(runtime, "_enableExperimentalFrameRecorder");
            DrawSection("Experimental",
                "Experimental features are hidden from both the Native Workspace and the Player when disabled.",
                () =>
                {
                    using (new EditorGUI.DisabledScope(!enableAgent.boolValue))
                    {
                        EditorGUILayout.PropertyField(frameRecorder,
                            new GUIContent("Frame Recorder",
                                "Enables Player-side rolling frame recording and the Frame Recorder workspace mode."));
                    }

                    if (frameRecorder.boolValue)
                    {
                        EditorGUILayout.HelpBox(
                            "Frame Recorder is experimental. Profile its CPU, GPU, and memory cost on each target platform before relying on it.",
                            MessageType.Warning);
                        if (!Property(runtime, "_allowRuntimeSceneInspector").boolValue)
                        {
                            EditorGUILayout.HelpBox(
                                "Remote Scene Inspector is disabled. Recorded images remain available, but recorded hierarchy and Inspector data will not be available.",
                                MessageType.Info);
                        }
                    }
                });

            SerializedProperty streamLogs = Property(runtime, "_streamLogs");
            DrawSection("Log Streaming Limits",
                "Bounds Player memory and the amount of log data sent in a single remote batch.",
                () =>
                {
                    using (new EditorGUI.DisabledScope(!enableAgent.boolValue || !streamLogs.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(runtime, "_maxQueuedLogs"),
                            new GUIContent("Maximum Queued Logs"));
                        EditorGUILayout.PropertyField(Property(runtime, "_maxLogsPerBatch"),
                            new GUIContent("Maximum Logs per Batch"));
                    }
                });
        }

        private static void DrawSceneInspectorSettings(SerializedProperty runtime)
        {
            SerializedProperty allowRemoteInspector =
                Property(runtime, "_allowRuntimeSceneInspector");
            SerializedProperty inspector = Property(runtime, "_runtimeSceneInspector");
            SerializedProperty enableInspector = Property(inspector, "m_EnableInspector");

            DrawSection("Availability",
                "Remote inspection and the optional in-game Inspector UI can be enabled independently.",
                () =>
                {
                    EditorGUILayout.PropertyField(allowRemoteInspector,
                        new GUIContent("Remote Inspector",
                            "Makes hierarchy, capture, object picking, and Inspector data available to the Editor workspace."));
                    EditorGUILayout.PropertyField(enableInspector,
                        new GUIContent("In-Game Inspector UI",
                            "Allows the Inspector interface to be shown inside the Player."));
                    using (new EditorGUI.DisabledScope(!enableInspector.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(inspector, "m_AutomaticallyCreateBootstrap"),
                            new GUIContent("Create In-Game UI Automatically"));
                    }
                });

            using (new EditorGUI.DisabledScope(!enableInspector.boolValue))
            {
                DrawSection("Behavior",
                    "Controls Player state and input while the runtime Inspector UI is open.",
                    () =>
                    {
                        EditorGUILayout.PropertyField(Property(inspector, "m_PauseWhenOpen"),
                            new GUIContent("Pause Player When Open"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_ConsumeInput"),
                            new GUIContent("Consume Player Input"));
                    });
            }

            DrawSection("Hierarchy",
                "Controls which objects appear and how frequently Inspector hierarchy data refreshes.",
                () =>
                {
                    EditorGUILayout.PropertyField(Property(inspector, "m_AutomaticRefresh"),
                        new GUIContent("Automatic Refresh"));
                    using (new EditorGUI.DisabledScope(!Property(inspector, "m_AutomaticRefresh").boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(inspector, "m_HierarchyRefreshInterval"),
                            new GUIContent("Refresh Interval (Seconds)"));
                    }
                    EditorGUILayout.PropertyField(Property(inspector, "m_IncludeInactiveObjects"),
                        new GUIContent("Include Inactive Objects"));
                });

            SerializedProperty allowPicking = Property(inspector, "m_AllowObjectPicking");
            DrawSection("Object Picking",
                "Controls which objects can be selected from a captured frame or the in-game Inspector.",
                () =>
                {
                    EditorGUILayout.PropertyField(allowPicking,
                        new GUIContent("Enable Object Picking"));
                    using (new EditorGUI.DisabledScope(!allowPicking.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(inspector, "m_ObjectPickingLayerMask"),
                            new GUIContent("Layer Mask"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_PickUiObjects"),
                            new GUIContent("Include UI Objects"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_PickTriggerColliders"),
                            new GUIContent("Include Trigger Colliders"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_UseRendererBoundsFallback"),
                            new GUIContent("Use Renderer Bounds Fallback"));
                    }
                });

            DrawSection("Editing Permissions",
                "Restricts changes that can be made through the in-game or remote Inspector.",
                () =>
                {
                    EditorGUILayout.PropertyField(Property(inspector, "m_AllowValueChanges"),
                        new GUIContent("Component Value Changes"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_AllowActivationChanges"),
                        new GUIContent("GameObject Activation Changes"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_AllowComponentEnableChanges"),
                        new GUIContent("Component Enable Changes"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_BlockedNamespaces"),
                        new GUIContent("Blocked Namespaces"), true);
                    EditorGUILayout.PropertyField(Property(inspector, "m_BlockedComponentTypes"),
                        new GUIContent("Blocked Component Types"), true);
                });

            DrawShaderInspectionSettings(inspector);

            using (new EditorGUI.DisabledScope(!enableInspector.boolValue))
            {
                DrawInspectorInputSettings(inspector);
                DrawInspectorAppearanceSettings(inspector);
            }
        }

        private static void DrawShaderInspectionSettings(SerializedProperty inspector)
        {
            SerializedProperty allowInspection = Property(inspector, "m_AllowShaderInspection");
            DrawSection("Shader Inspection",
                "Shader data can be expensive to enumerate. Keep this disabled unless material or shader debugging is required.",
                () =>
                {
                    EditorGUILayout.PropertyField(allowInspection,
                        new GUIContent("Enable Shader Inspection"));
                    using (new EditorGUI.DisabledScope(!allowInspection.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(inspector, "m_AllowShaderValueChanges"),
                            new GUIContent("Shader Value Changes"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_AllowMaterialPropertyBlockChanges"),
                            new GUIContent("Material Property Block Changes"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_AllowMaterialInstantiation"),
                            new GUIContent("Material Instantiation"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_AllowSharedMaterialChanges"),
                            new GUIContent("Shared Material Changes"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_AllowGlobalShaderChanges"),
                            new GUIContent("Global Shader Changes"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_AllowTextureChanges"),
                            new GUIContent("Texture Changes"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_ShowHiddenShaderProperties"),
                            new GUIContent("Show Hidden Shader Properties"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_MaxInspectorMaterialInstances"),
                            new GUIContent("Maximum Material Instances"));
                        EditorGUILayout.PropertyField(Property(inspector, "m_MaxVisibleShaderProperties"),
                            new GUIContent("Maximum Visible Shader Properties"));
                    }
                });
        }

        private static void DrawInspectorInputSettings(SerializedProperty inspector)
        {
            DrawSection("Input & Editing",
                "Configures numeric editing increments and controller navigation for the runtime Inspector UI.",
                () =>
                {
                    EditorGUILayout.PropertyField(Property(inspector, "m_NormalNumericStep"),
                        new GUIContent("Normal Numeric Step"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_LargeNumericStep"),
                        new GUIContent("Large Numeric Step"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_SmallNumericStep"),
                        new GUIContent("Small Numeric Step"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_NavigationRepeatDelay"),
                        new GUIContent("Navigation Repeat Delay"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_NavigationRepeatRate"),
                        new GUIContent("Navigation Repeat Rate"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_ControllerDeadZone"),
                        new GUIContent("Controller Dead Zone"));
                });
        }

        private static void DrawInspectorAppearanceSettings(SerializedProperty inspector)
        {
            DrawSection("Appearance & Fonts",
                "Controls the runtime Inspector UI. These values do not change the Native Workspace layout.",
                () =>
                {
                    EditorGUILayout.PropertyField(Property(inspector, "m_UiScale"),
                        new GUIContent("UI Scale"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_BackgroundColor"),
                        new GUIContent("Background Color"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_FocusColor"),
                        new GUIContent("Focus Color"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_HierarchyPanelWidth"),
                        new GUIContent("Hierarchy Panel Width"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_RegularFont"),
                        new GUIContent("Regular Font"));
                    EditorGUILayout.PropertyField(Property(inspector, "m_BoldFont"),
                        new GUIContent("Bold Font"));
                });
        }

        private static void DrawNetworkSettings(SerializedProperty runtime)
        {
            DrawSection("TCP Transport",
                "Configures the direct connection used by Remote Dev Utilities.",
                () =>
                {
                    EditorGUILayout.PropertyField(Property(runtime, "_tcpPort"),
                        new GUIContent("TCP Port"));
                    SerializedProperty fallback = Property(runtime, "_enableTcpPortFallback");
                    EditorGUILayout.PropertyField(fallback,
                        new GUIContent("Enable Port Fallback"));
                    using (new EditorGUI.DisabledScope(!fallback.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(runtime, "_tcpPortFallbackCount"),
                            new GUIContent("Additional Ports to Try"));
                    }
                });

            SerializedProperty allowRemote =
                Property(runtime, "_allowTcpConnectionsFromOtherMachines");
            SerializedProperty accessToken = Property(runtime, "_tcpAccessToken");
            DrawSection("Remote Access & Security",
                "Remote TCP traffic is not encrypted. Enable remote access only on a trusted development network.",
                () =>
                {
                    EditorGUILayout.PropertyField(allowRemote,
                        new GUIContent("Allow Other Machines"));
                    DrawAccessToken(accessToken);
                    if (allowRemote.boolValue && string.IsNullOrWhiteSpace(accessToken.stringValue))
                    {
                        EditorGUILayout.HelpBox(
                            "Remote access requires a non-empty access token.",
                            MessageType.Warning);
                    }
                });

            SerializedProperty discovery = Property(runtime, "_enableLanDiscovery");
            DrawSection("LAN Discovery",
                "Advertises the Player to Remote Dev Utilities Editors on the local network.",
                () =>
                {
                    EditorGUILayout.PropertyField(discovery,
                        new GUIContent("Enable LAN Discovery"));
                    using (new EditorGUI.DisabledScope(!discovery.boolValue))
                    {
                        EditorGUILayout.PropertyField(Property(runtime, "_enableLanDiscoveryDiagnosticLogs"),
                            new GUIContent("Diagnostic Logs"));
                    }

                    if (discovery.boolValue &&
                        (!allowRemote.boolValue || string.IsNullOrWhiteSpace(accessToken.stringValue)))
                    {
                        EditorGUILayout.HelpBox(
                            "LAN discovery will remain inactive until Allow Other Machines is enabled and an access token is set.",
                            MessageType.Warning);
                    }
                });
        }

        private static void DrawAccessToken(SerializedProperty accessToken)
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect labelRect = new(row.x, row.y, EditorGUIUtility.labelWidth, row.height);
            Rect showRect = new(row.xMax - 48f, row.y, 48f, row.height);
            Rect fieldRect = new(labelRect.xMax, row.y,
                Mathf.Max(40f, showRect.xMin - labelRect.xMax - 4f), row.height);
            EditorGUI.LabelField(labelRect,
                new GUIContent("Access Token", "Required when accepting TCP connections from other machines."));
            string value = s_ShowAccessToken
                ? EditorGUI.TextField(fieldRect, accessToken.stringValue)
                : EditorGUI.PasswordField(fieldRect, accessToken.stringValue);
            if (value != accessToken.stringValue)
                accessToken.stringValue = value;
            s_ShowAccessToken = GUI.Toggle(showRect, s_ShowAccessToken, "Show", EditorStyles.miniButton);
        }

        private static void DrawSection(string title, string description, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(3f);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private static SerializedProperty Property(SerializedProperty parent, string relativeName)
        {
            SerializedProperty property = parent?.FindPropertyRelative(relativeName);
            if (property == null)
                throw new System.InvalidOperationException(
                    $"Remote Dev Utilities setting '{relativeName}' could not be found.");
            return property;
        }
    }
}

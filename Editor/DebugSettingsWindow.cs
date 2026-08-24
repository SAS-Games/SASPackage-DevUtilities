using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    /// <summary>
    /// Compatibility entry point for the former standalone Debug Settings window.
    /// Debug configuration now lives with the other project-scoped Dev Utilities settings.
    /// </summary>
    public class DebugSettingsWindow : EditorWindow
    {
        internal const string SettingsPath = "Project/Dev Utilities/Debug";

        [MenuItem("Tools/Dev Utilities/Debug Settings")]
        public static void ShowWindow()
        {
            SettingsService.OpenProjectSettings(SettingsPath);
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
            {
                label = "Debug",
                guiHandler = _ => DrawSettings(),
                keywords = new HashSet<string>
                {
                    "Debug",
                    "ENABLE_DEBUG",
                    "Logging",
                    "Log Level",
                    "Allowed Tags",
                    "Developer Console",
                    "Pause"
                }
            };
        }

        private static void DrawSettings()
        {
            DebugEditorSettings settings = DebugEditorSettings.instance;

            EditorGUILayout.HelpBox(
                "Debug configuration is stored in ProjectSettings and baked into Players built with " +
                "ENABLE_DEBUG. Runtime snapshots are generated only for the build and cleaned up afterwards.",
                MessageType.Info);

            DrawSection("Build",
                "ENABLE_DEBUG is configured independently for the currently selected build target.",
                DrawEnableDebugSetting);

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.Update();

            bool changed;
            EditorGUI.BeginChangeCheck();
            DrawSection("Developer Console",
                "Controls console behavior when it is opened in the Editor or an ENABLE_DEBUG Player.",
                () => EditorGUILayout.PropertyField(
                    serializedSettings.FindProperty("pauseOnEnable"),
                    new GUIContent("Pause On Enable")));
            DrawSection("Logging",
                "Choose the log levels and optional tags accepted by the Dev Utilities logger.",
                () =>
                {
                    EditorGUILayout.PropertyField(
                        serializedSettings.FindProperty("logLevel"),
                        new GUIContent("Log Level"));
                    EditorGUILayout.PropertyField(
                        serializedSettings.FindProperty("allowedTags"),
                        new GUIContent("Allowed Tags"),
                        true);
                });
            changed = EditorGUI.EndChangeCheck();

            if (!changed)
                return;

            serializedSettings.ApplyModifiedProperties();
            settings.allowedTags ??= new List<string>();
            settings.SaveSettings();

            if (Application.isPlaying)
                DebugSettings.ApplyFromEditor();
        }

        private static void DrawEnableDebugSetting()
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            bool hasValidTarget = targetGroup != BuildTargetGroup.Unknown;

            using (new EditorGUI.DisabledScope(!hasValidTarget))
            {
                bool enabled = hasValidTarget && DefineSymbolsMenu.HasSymbol("ENABLE_DEBUG");
                EditorGUI.BeginChangeCheck();
                enabled = EditorGUILayout.Toggle(
                    new GUIContent("Enable Debug (ENABLE_DEBUG)",
                        "Adds or removes ENABLE_DEBUG for the selected build target."),
                    enabled);
                if (EditorGUI.EndChangeCheck())
                    DefineSymbolsMenu.ModifyDefineSymbols("ENABLE_DEBUG", enabled);
            }

            string targetName = hasValidTarget
                ? ObjectNames.NicifyVariableName(targetGroup.ToString())
                : "No valid build target selected";
            EditorGUILayout.LabelField($"Selected target: {targetName}", EditorStyles.miniLabel);
        }

        private static void DrawSection(string title, string description, System.Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(description))
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(3f);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Debug Settings have moved to Project Settings > Dev Utilities > Debug.",
                MessageType.Info);

            if (GUILayout.Button("Open Project Settings"))
                ShowWindow();
        }
    }
}

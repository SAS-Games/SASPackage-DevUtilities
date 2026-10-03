using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SAS.Utilities.DeveloperConsole.Editor
{
    /// <summary>
    /// Compatibility entry point for the former standalone Debug Settings window.
    /// Debug configuration now lives with the other project-scoped Dev Utilities settings.
    /// </summary>
    public class DebugSettingsWindow : EditorWindow
    {
        internal const string SettingsPath = "Project/Dev Utilities/Debug";
        private const string ConsolePrefabRelativePath =
            "Runtime/DeveloperConsole/Assets/Resources/ConsoleCommandsSystem.prefab";

        private static string[] s_BuiltInCommandNames;

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
                    "Console Toggle",
                    "Keyboard",
                    "Gamepad",
                    "DualShock",
                    "DualSense",
                    "Touchpad",
                    "Touchscreen",
                    "Mobile",
                    "Multi-touch",
                    "Key Combination",
                    "Hold Duration",
                    "Pause",
                    "Command Visibility",
                    "Package Commands",
                    "Remote Commands"
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
                    new GUIContent(
                        "Pause When Developer Console Opens",
                        "Pauses the Player while the Developer Console is open.")));
            DrawConsoleInputSettings(serializedSettings.FindProperty("consoleInput"));
            DrawBuiltInCommandVisibility(
                serializedSettings.FindProperty("hiddenConsoleCommands"));
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

        private static void DrawBuiltInCommandVisibility(SerializedProperty hiddenCommands)
        {
            DrawSection("Built-In Command Visibility",
                "Choose which package commands are discoverable in the local in-game console. " +
                "Hidden commands stay registered and remain available to Remote Dev Utilities.",
                () =>
                {
                    if (hiddenCommands == null)
                    {
                        EditorGUILayout.HelpBox(
                            "Command visibility settings could not be loaded.",
                            MessageType.Error);
                        return;
                    }

                    string[] commandNames = GetBuiltInCommandNames();
                    if (commandNames.Length == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "The built-in Developer Console commands could not be discovered. " +
                            "Hidden command names can still be edited manually.",
                            MessageType.Warning);
                        EditorGUILayout.PropertyField(
                            hiddenCommands,
                            new GUIContent("Hidden Command Names"),
                            true);
                        return;
                    }

                    EditorGUILayout.LabelField("Show in Local Console", EditorStyles.miniBoldLabel);
                    foreach (string commandName in commandNames)
                    {
                        bool isVisible = !Contains(hiddenCommands, commandName);
                        bool nextVisible = EditorGUILayout.ToggleLeft(commandName, isVisible);
                        if (nextVisible != isVisible)
                            SetHidden(hiddenCommands, commandName, !nextVisible);
                    }

                    GUILayout.Space(3f);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Show All"))
                    {
                        hiddenCommands.arraySize = 0;
                        GUI.changed = true;
                    }

                    if (GUILayout.Button("Hide All"))
                    {
                        hiddenCommands.arraySize = commandNames.Length;
                        for (int i = 0; i < commandNames.Length; i++)
                            hiddenCommands.GetArrayElementAtIndex(i).stringValue = commandNames[i];
                        GUI.changed = true;
                    }
                    EditorGUILayout.EndHorizontal();
                });
        }

        private static string[] GetBuiltInCommandNames()
        {
            if (s_BuiltInCommandNames != null)
                return s_BuiltInCommandNames;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            PackageManagerPackageInfo package =
                PackageManagerPackageInfo.FindForAssembly(typeof(DebugSettingsWindow).Assembly);
            if (package == null || string.IsNullOrWhiteSpace(package.assetPath))
                return s_BuiltInCommandNames = Array.Empty<string>();

            string prefabPath = $"{package.assetPath}/{ConsolePrefabRelativePath}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            DeveloperConsoleBehaviour console = prefab != null
                ? prefab.GetComponentInChildren<DeveloperConsoleBehaviour>(true)
                : null;
            if (console == null)
                return s_BuiltInCommandNames = Array.Empty<string>();

            var serializedConsole = new SerializedObject(console);
            AddCommandNames(serializedConsole.FindProperty("m_Commands"), names);

            SerializedProperty platformCommands =
                serializedConsole.FindProperty("m_PlatformCommands");
            if (platformCommands != null && platformCommands.isArray)
            {
                for (int i = 0; i < platformCommands.arraySize; i++)
                {
                    AddCommandNames(
                        platformCommands.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("commands"),
                        names);
                }
            }

            var result = new List<string>(names);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return s_BuiltInCommandNames = result.ToArray();
        }

        private static void AddCommandNames(
            SerializedProperty commands,
            HashSet<string> names)
        {
            if (commands == null || !commands.isArray)
                return;

            for (int i = 0; i < commands.arraySize; i++)
            {
                var command = commands.GetArrayElementAtIndex(i).objectReferenceValue
                    as ConsoleCommand;
                if (command != null && !string.IsNullOrWhiteSpace(command.Name))
                    names.Add(command.Name.Trim());
            }
        }

        private static bool Contains(SerializedProperty values, string value)
        {
            for (int i = 0; i < values.arraySize; i++)
            {
                if (string.Equals(
                    values.GetArrayElementAtIndex(i).stringValue,
                    value,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetHidden(
            SerializedProperty hiddenCommands,
            string commandName,
            bool hidden)
        {
            for (int i = 0; i < hiddenCommands.arraySize; i++)
            {
                if (!string.Equals(
                    hiddenCommands.GetArrayElementAtIndex(i).stringValue,
                    commandName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (hidden)
                    return;

                hiddenCommands.DeleteArrayElementAtIndex(i);
                return;
            }

            if (!hidden)
                return;

            int index = hiddenCommands.arraySize;
            hiddenCommands.InsertArrayElementAtIndex(index);
            hiddenCommands.GetArrayElementAtIndex(index).stringValue = commandName;
        }

        private static void DrawConsoleInputSettings(SerializedProperty inputSettings)
        {
            DrawSection("Console Toggle Input",
                "Every item in a combination must be held together. Set a hold duration to 0 for an immediate toggle.",
                () =>
                {
                    if (inputSettings == null)
                    {
                        EditorGUILayout.HelpBox("Console input settings could not be loaded.", MessageType.Error);
                        return;
                    }

                    EditorGUILayout.PropertyField(
                        inputSettings.FindPropertyRelative("m_KeyboardKeys"),
                        new GUIContent("Keyboard Combination"),
                        true);
                    EditorGUILayout.PropertyField(
                        inputSettings.FindPropertyRelative("m_KeyboardHoldDuration"),
                        new GUIContent("Keyboard Hold Time", "Seconds the full keyboard combination must remain held."));
                    GUILayout.Space(3f);
                    EditorGUILayout.PropertyField(
                        inputSettings.FindPropertyRelative("m_GamepadButtons"),
                        new GUIContent("Controller Combination"),
                        true);
                    EditorGUILayout.PropertyField(
                        inputSettings.FindPropertyRelative("m_RequireGamepadTouchpad"),
                        new GUIContent("Require Touchpad Click", "Adds the DualShock/DualSense touchpad click to the controller combination. An empty button list makes touchpad click the complete shortcut."));
                    EditorGUILayout.PropertyField(
                        inputSettings.FindPropertyRelative("m_GamepadHoldDuration"),
                        new GUIContent("Controller Hold Time", "Seconds the full controller combination must remain held."));

                    GUILayout.Space(3f);
                    SerializedProperty enableTouchscreen =
                        inputSettings.FindPropertyRelative("m_EnableTouchscreenGesture");
                    EditorGUILayout.PropertyField(
                        enableTouchscreen,
                        new GUIContent("Enable Touchscreen Gesture", "Allows a multi-finger hold to toggle the console on mobile or other touchscreen devices."));
                    using (new EditorGUI.DisabledScope(!enableTouchscreen.boolValue))
                    {
                        EditorGUILayout.PropertyField(
                            inputSettings.FindPropertyRelative("m_TouchscreenTouchCount"),
                            new GUIContent("Minimum Touch Count", "Minimum number of fingers that must remain on the screen."));
                        EditorGUILayout.PropertyField(
                            inputSettings.FindPropertyRelative("m_TouchscreenHoldDuration"),
                            new GUIContent("Touchscreen Hold Time", "Seconds the multi-finger gesture must remain held."));
                    }
                });
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

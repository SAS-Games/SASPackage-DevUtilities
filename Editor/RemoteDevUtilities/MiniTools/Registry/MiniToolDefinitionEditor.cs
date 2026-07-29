using System;
using System.Collections.Generic;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    [CustomEditor(typeof(MiniToolDefinition))]
    internal sealed class MiniToolDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _toolId;
        private SerializedProperty _displayName;
        private SerializedProperty _description;
        private SerializedProperty _updateInterval;
        private SerializedProperty _providerTypeName;
        private SerializedProperty _command;
        private SerializedProperty _commandName;
        private SerializedProperty _commandRouting;
        private SerializedProperty _debugHostPrefabGuid;
        private SerializedProperty _visibleByDefault;

        private void OnEnable()
        {
            _toolId = serializedObject.FindProperty("_toolId");
            _displayName = serializedObject.FindProperty("_displayName");
            _description = serializedObject.FindProperty("_description");
            _updateInterval =
                serializedObject.FindProperty("_updateInterval");
            _providerTypeName =
                serializedObject.FindProperty("_providerTypeName");
            _command = serializedObject.FindProperty("_command");
            _commandName =
                serializedObject.FindProperty("_commandName");
            _commandRouting =
                serializedObject.FindProperty("_commandRouting");
            _debugHostPrefabGuid =
                serializedObject.FindProperty("_debugHostPrefabGuid");
            _visibleByDefault =
                serializedObject.FindProperty("_visibleByDefault");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent(
                        "Tool ID",
                        "Generated once and shared automatically by every mini-tool consumer."),
                    _toolId.stringValue);
            }
            if (GUILayout.Button(
                    "Regenerate",
                    EditorStyles.miniButton,
                    GUILayout.Width(76f)) &&
                EditorUtility.DisplayDialog(
                    "Regenerate Mini-Tool ID?",
                    "Existing project overrides and older builds will no longer identify this as the same tool.",
                    "Regenerate",
                    "Cancel"))
            {
                _toolId.stringValue =
                    $"mini-tool.{Guid.NewGuid():N}";
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_description);
            EditorGUILayout.PropertyField(
                _updateInterval,
                new GUIContent("Update Interval"));
            DrawProvider();
            EditorGUILayout.Space(4f);
            DrawCommand();
            using (new EditorGUI.DisabledScope(
                       _command.objectReferenceValue == null))
            {
                EditorGUILayout.PropertyField(
                    _commandRouting,
                    new GUIContent("When Command Runs"));
            }

            DrawDebugHostPrefab();
            EditorGUILayout.PropertyField(
                _visibleByDefault,
                new GUIContent("Visible by Default"));

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(target);
                MiniToolRegistry.Invalidate();
            }

            MiniToolDefinition definition =
                (MiniToolDefinition)target;
            if (!definition.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }

            var presentationErrors = new List<string>();
            var presentationWarnings = new List<string>();
            MiniToolRegistrationValidator.Validate(
                definition,
                string.Empty,
                presentationErrors,
                presentationWarnings);
            foreach (string presentationError in presentationErrors)
            {
                EditorGUILayout.HelpBox(
                    presentationError,
                    MessageType.Error);
            }
            foreach (string presentationWarning in presentationWarnings)
            {
                EditorGUILayout.HelpBox(
                    presentationWarning,
                    MessageType.Warning);
            }
        }

        private void DrawProvider()
        {
            MonoScript currentScript = FindProviderScript(
                _providerTypeName.stringValue);
            EditorGUI.BeginChangeCheck();
            MonoScript selectedScript =
                (MonoScript)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Data Provider",
                        $"A concrete {nameof(IMiniToolDataProvider)} implementation. " +
                        $"Native Workspace fields ({nameof(IMiniToolFieldProvider)}) " +
                        "and typed Debug Host snapshots are optional capabilities."),
                    currentScript,
                    typeof(MonoScript),
                    false);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (selectedScript == null)
            {
                _providerTypeName.stringValue = string.Empty;
                return;
            }

            Type type = selectedScript.GetClass();
            if (type == null ||
                !typeof(IMiniToolDataProvider).IsAssignableFrom(type))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Data Provider",
                    $"Select a script implementing {nameof(IMiniToolDataProvider)}.",
                    "OK");
                return;
            }

            _providerTypeName.stringValue =
                $"{type.FullName}, {type.Assembly.GetName().Name}";
        }

        private void DrawDebugHostPrefab()
        {
            string path = AssetDatabase.GUIDToAssetPath(
                _debugHostPrefabGuid.stringValue);
            GameObject current = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(path);

            EditorGUI.BeginChangeCheck();
            GameObject selected =
                (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Debug Host Prefab",
                        "Optional Editor presentation. Its GUID is stored so the prefab is not included in Player builds."),
                    current,
                    typeof(GameObject),
                    false);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (selected == null)
            {
                _debugHostPrefabGuid.stringValue = string.Empty;
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selected);
            if (!selectedPath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Debug Host Prefab",
                    "Select a prefab asset.",
                    "OK");
                return;
            }

            _debugHostPrefabGuid.stringValue =
                AssetDatabase.AssetPathToGUID(selectedPath);
        }

        private void DrawCommand()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(
                _command,
                new GUIContent(
                    "Command",
                    "Optional command asset that starts or stops this Editor tool."));
            bool commandChanged = EditorGUI.EndChangeCheck();
            if (commandChanged &&
                _command.objectReferenceValue == null)
            {
                _commandName.stringValue = string.Empty;
            }

            var command =
                _command.objectReferenceValue as ConsoleCommand;
            if (command == null)
                return;

            List<string> actions = GetCommandActions(command);
            if (actions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "The selected command does not expose an executable action.",
                    MessageType.Error);
                return;
            }
            if (commandChanged)
                _commandName.stringValue = actions[0];

            string current = string.IsNullOrWhiteSpace(
                _commandName.stringValue)
                ? command.Name
                : _commandName.stringValue;
            int currentIndex = actions.FindIndex(
                action => string.Equals(
                    action,
                    current,
                    StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                actions.Insert(0, current);
                currentIndex = 0;
            }

            int selectedIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Command Action",
                    "Select the root command or one of its declared subcommands."),
                currentIndex,
                actions.ToArray());
            _commandName.stringValue = actions[selectedIndex];
        }

        private static List<string> GetCommandActions(
            ConsoleCommand command)
        {
            var actions = new List<string>();
            var serializedCommand = new SerializedObject(command);
            SerializedProperty subCommands =
                serializedCommand.FindProperty("m_SubCommands");
            if (subCommands == null ||
                !subCommands.isArray ||
                subCommands.arraySize == 0)
            {
                if (!string.IsNullOrWhiteSpace(command.Name))
                    actions.Add(command.Name);
                return actions;
            }

            for (int i = 0; i < subCommands.arraySize; i++)
            {
                SerializedProperty entry =
                    subCommands.GetArrayElementAtIndex(i);
                string subCommand =
                    entry.FindPropertyRelative("Name")?.stringValue;
                if (string.IsNullOrWhiteSpace(subCommand))
                    continue;

                string action = $"{command.Name}.{subCommand}";
                if (!actions.Contains(action))
                    actions.Add(action);
            }

            return actions;
        }

        private static MonoScript FindProviderScript(string typeName)
        {
            Type type = string.IsNullOrWhiteSpace(typeName)
                ? null
                : Type.GetType(typeName, false);
            if (type == null)
                return null;

            foreach (MonoScript script in
                     MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script != null && script.GetClass() == type)
                    return script;
            }

            return null;
        }
    }
}

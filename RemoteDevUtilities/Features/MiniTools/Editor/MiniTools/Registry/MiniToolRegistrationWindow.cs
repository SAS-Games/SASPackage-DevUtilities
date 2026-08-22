using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    internal sealed class MiniToolRegistrationWindow : EditorWindow
    {
        private enum MiniToolWorkflow
        {
            CreateNew,
            RegisterExisting
        }

        private enum MiniToolSetupTarget
        {
            [InspectorName("Debug Host (Shared Prefab UI)")] DebugHost,

            [InspectorName("Native Workspace Fields")] NativeWorkspaceFields
        }

        private string _toolName = "New Mini Tool";
        private MonoScript _existingProvider;
        private MiniToolSetupTarget _setupTarget = MiniToolSetupTarget.DebugHost;
        private bool _generateProvider = true;
        private ConsoleCommand _command;
        private string _commandAction = string.Empty;
        private RemoteCommandRouting _routing = RemoteCommandRouting.ControlEditorToolOnly;
        private GameObject _debugHostPrefab;
        private GameObject _snapshotContractPrefab;
        private Type[] _snapshotTypes = Array.Empty<Type>();
        private int _snapshotTypeIndex;
        private float _updateInterval = 1f;
        private bool _visibleByDefault = true;
        [SerializeField] private MiniToolWorkflow _workflow = MiniToolWorkflow.CreateNew;
        [SerializeField] private MiniToolScaffoldForm _scaffoldForm = new();

        [MenuItem("Assets/Create/HP/Dev Utilities/Mini Tool...", priority = 120)]
        [MenuItem("Tools/Dev Utilities/Create Mini Tool...", priority = 120)]
        private static void OpenCreateFromMenu()
        {
            OpenWindow(MiniToolWorkflow.CreateNew);
        }

        [MenuItem("Tools/Dev Utilities/Register Existing Mini Tool...", priority = 121)]
        private static void OpenRegistrationFromMenu()
        {
            OpenWindow(MiniToolWorkflow.RegisterExisting);
        }

        internal static void OpenWindow()
        {
            OpenWindow(MiniToolWorkflow.CreateNew);
        }

        private static void OpenWindow(MiniToolWorkflow workflow)
        {
            var window = GetWindow<MiniToolRegistrationWindow>(true, "Mini Tool Setup", true);
            window._workflow = workflow;
            window.minSize = new Vector2(520f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_debugHostPrefab == null && Selection.activeObject is GameObject selectedPrefab && IsPrefabAsset(selectedPrefab))
            {
                _debugHostPrefab = selectedPrefab;
                if (string.Equals(_toolName, "New Mini Tool", StringComparison.Ordinal))
                    _toolName = selectedPrefab.name;
            }

            _scaffoldForm ??= new MiniToolScaffoldForm();
            _scaffoldForm.InitializeFromSelection();
            RefreshSnapshotTypes();
        }

        private void OnGUI()
        {
            _workflow = (MiniToolWorkflow)GUILayout.Toolbar((int)_workflow, new[] { "Create New Mini Tool", "Register Existing Mini Tool" });
            EditorGUILayout.Space(10f);
            if (_workflow == MiniToolWorkflow.CreateNew)
            {
                if (_scaffoldForm.Draw())
                    Close();
                return;
            }

            EditorGUILayout.LabelField("Mini Tool Registration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Registers one definition shared by the Player, Debug Host, optional Native Workspace presentation, and command routing.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8f);

            _toolName = EditorGUILayout.TextField("Tool Name", _toolName);
            _setupTarget = (MiniToolSetupTarget)EditorGUILayout.EnumPopup("Setup For", _setupTarget);
            DrawProviderSetup();

            _updateInterval = EditorGUILayout.FloatField("Update Interval", _updateInterval);
            DrawCommandSetup();
            _visibleByDefault = EditorGUILayout.Toggle("Visible by Default", _visibleByDefault);

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(!CanRegister()))
            {
                if (GUILayout.Button("Register Mini Tool", GUILayout.Height(28f)))
                {
                    RegisterMiniTool();
                }
            }
        }

        private void DrawProviderSetup()
        {
            bool debugHost = _setupTarget == MiniToolSetupTarget.DebugHost;
            string generateLabel = debugHost ? "Generate Matching Snapshot Provider" : "Generate Native Workspace Field Provider";
            _generateProvider = EditorGUILayout.ToggleLeft(generateLabel, _generateProvider);
            using (new EditorGUI.DisabledScope(_generateProvider))
            {
                _existingProvider = (MonoScript)EditorGUILayout.ObjectField("Existing Data Provider", _existingProvider, typeof(MonoScript), false);
            }

            if (debugHost)
            {
                EditorGUILayout.HelpBox("The Debug Host reuses this prefab UI. A component on the prefab must implement IMiniToolSnapshotView<TSnapshot>.", MessageType.Info);
                DrawDebugHostSnapshotSetup();
            }
            else
                _debugHostPrefab = (GameObject)EditorGUILayout.ObjectField("Optional Debug Host Prefab", _debugHostPrefab, typeof(GameObject), false);
        }

        private void DrawDebugHostSnapshotSetup()
        {
            if (_snapshotContractPrefab != _debugHostPrefab)
                RefreshSnapshotTypes();

            EditorGUI.BeginChangeCheck();
            _debugHostPrefab = (GameObject)EditorGUILayout.ObjectField("Debug Host Prefab", _debugHostPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                RefreshSnapshotTypes();

            if (_debugHostPrefab == null)
            {
                EditorGUILayout.HelpBox("Assign the existing mini-tool prefab.", MessageType.Warning);
                return;
            }

            if (_snapshotTypes.Length == 0)
            {
                EditorGUILayout.HelpBox("No IMiniToolSnapshotView<TSnapshot> component was found on the prefab or its children.", MessageType.Error);
                return;
            }

            if (_debugHostPrefab.GetComponent<DevUtilityPresentation>() == null)
            {
                EditorGUILayout.HelpBox("Add DevUtilityPresentation to the prefab root. It preserves the tool's requested visibility when build UI is suppressed by a remote connection.", MessageType.Error);
                return;
            }

            if (_generateProvider)
            {
                var options = new string[_snapshotTypes.Length];
                for (int i = 0; i < _snapshotTypes.Length; i++)
                {
                    options[i] = _snapshotTypes[i].FullName ?? _snapshotTypes[i].Name;
                }

                _snapshotTypeIndex = EditorGUILayout.Popup("Snapshot Contract", Mathf.Clamp(_snapshotTypeIndex, 0, _snapshotTypes.Length - 1), options);
                EditorGUILayout.HelpBox("The generated provider compiles immediately but returns no data until TryGetSnapshot is connected to the Player's collector.", MessageType.Info);
                return;
            }

            Type providerType = _existingProvider?.GetClass();
            if (IsProvider(providerType) && !MiniToolSnapshotContractDiscovery.HasCompatibleSnapshot(providerType, _snapshotTypes))
            {
                EditorGUILayout.HelpBox("The selected provider does not expose a snapshot consumed by this prefab.", MessageType.Error);
            }
        }

        private void DrawCommandSetup()
        {
            EditorGUI.BeginChangeCheck();
            _command = (ConsoleCommand)EditorGUILayout.ObjectField("Command", _command, typeof(ConsoleCommand), false);
            if (EditorGUI.EndChangeCheck())
                _commandAction = string.Empty;

            List<string> actions = GetCommandActions(_command);
            if (actions.Count > 0)
            {
                int selectedIndex = actions.FindIndex(action => string.Equals(action, _commandAction, StringComparison.OrdinalIgnoreCase));
                if (selectedIndex < 0)
                    selectedIndex = 0;
                selectedIndex = EditorGUILayout.Popup("Command Action", selectedIndex, actions.ToArray());
                _commandAction = actions[selectedIndex];
            }
            else
                _commandAction = string.Empty;

            using (new EditorGUI.DisabledScope(_command == null))
            {
                _routing = (RemoteCommandRouting)EditorGUILayout.EnumPopup("When Command Runs", _routing);
            }

            if (_setupTarget == MiniToolSetupTarget.DebugHost && _command == null)
            {
                EditorGUILayout.HelpBox("Assign the On/Off command used to subscribe and open this tool from the Debug Host console.", MessageType.Warning);
            }
        }

        private bool CanRegister()
        {
            if (string.IsNullOrWhiteSpace(_toolName))
                return false;

            Type providerType = _existingProvider?.GetClass();
            if (_setupTarget == MiniToolSetupTarget.NativeWorkspaceFields)
                return _generateProvider || IsProvider(providerType);

            if (!IsPrefabAsset(_debugHostPrefab) || _snapshotTypes.Length == 0 || _debugHostPrefab.GetComponent<DevUtilityPresentation>() == null || _command == null || string.IsNullOrWhiteSpace(_commandAction))
                return false;

            if (_generateProvider)
                return GetSelectedSnapshotType() != null;

            return IsProvider(providerType) && MiniToolSnapshotContractDiscovery.HasCompatibleSnapshot(providerType, _snapshotTypes);
        }

        private void RegisterMiniTool()
        {
            string folder = GetSelectedFolder();
            if (IsEditorOnlyPath(folder))
            {
                EditorUtility.DisplayDialog("Choose a Runtime Folder", "Mini Tool Definitions and data providers must be created outside an Editor folder so they can be included in a Player build.", "OK");
                return;
            }

            string classStem = ToIdentifier(_toolName);
            string providerTypeName;
            string providerScriptGuid;

            if (_generateProvider)
            {
                string scriptPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{classStem}DataProvider.cs");
                string className = ToIdentifier(Path.GetFileNameWithoutExtension(scriptPath));
                string source = _setupTarget == MiniToolSetupTarget.DebugHost ? MiniToolProviderTemplateGenerator.CreateSnapshotProvider(className, GetSelectedSnapshotType()) : MiniToolProviderTemplateGenerator.CreateFieldProvider(className);
                File.WriteAllText(ToAbsolutePath(scriptPath), source, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(scriptPath);
                providerScriptGuid = AssetDatabase.AssetPathToGUID(scriptPath);
                string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
                if (string.IsNullOrWhiteSpace(assemblyName))
                    assemblyName = "Assembly-CSharp";
                else
                    assemblyName = Path.GetFileNameWithoutExtension(assemblyName);
                providerTypeName = $"{className}, {assemblyName}";
            }
            else
            {
                Type providerType = _existingProvider.GetClass();
                providerTypeName = $"{providerType.FullName}, {providerType.Assembly.GetName().Name}";
                providerScriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_existingProvider));
            }

            var definition = CreateInstance<MiniToolDefinition>();
            string definitionPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{classStem} Mini Tool.asset");
            AssetDatabase.CreateAsset(definition, definitionPath);

            var serialized = new SerializedObject(definition);
            string slug = ToSlug(_toolName);
            if (string.IsNullOrWhiteSpace(slug))
                slug = "tool";
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            serialized.FindProperty("_toolId").stringValue = $"custom.{slug}.{suffix}";
            serialized.FindProperty("_displayName").stringValue = _toolName.Trim();
            serialized.FindProperty("_updateInterval").floatValue = Mathf.Max(0.1f, _updateInterval);
            serialized.FindProperty("_providerScriptGuid").stringValue = providerScriptGuid;
            serialized.FindProperty("_providerTypeName").stringValue = providerTypeName;
            serialized.FindProperty("_command").objectReferenceValue = _command;
            serialized.FindProperty("_commandName").stringValue = _commandAction;
            serialized.FindProperty("_commandRouting").enumValueIndex = (int)_routing;
            serialized.FindProperty("_visibleByDefault").boolValue = _visibleByDefault;
            if (_debugHostPrefab != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(_debugHostPrefab);
                serialized.FindProperty("_debugHostPrefabGuid").stringValue = AssetDatabase.AssetPathToGUID(prefabPath);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MiniToolRegistry.Invalidate();
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
            Close();
        }

        private static bool IsProvider(Type type)
        {
            return type != null && type.IsClass && !type.IsAbstract && typeof(IMiniToolDataProvider).IsAssignableFrom(type);
        }

        private static List<string> GetCommandActions(ConsoleCommand command)
        {
            var actions = new List<string>();
            if (command == null)
                return actions;

            var serializedCommand = new SerializedObject(command);
            SerializedProperty subCommands = serializedCommand.FindProperty("m_SubCommands");
            if (subCommands == null || !subCommands.isArray || subCommands.arraySize == 0)
            {
                if (!string.IsNullOrWhiteSpace(command.Name))
                    actions.Add(command.Name);
                return actions;
            }

            for (int i = 0; i < subCommands.arraySize; i++)
            {
                SerializedProperty entry = subCommands.GetArrayElementAtIndex(i);
                string subCommand = entry.FindPropertyRelative("Name")?.stringValue;
                if (string.IsNullOrWhiteSpace(subCommand))
                    continue;

                string action = $"{command.Name}.{subCommand}";
                if (!actions.Contains(action))
                    actions.Add(action);
            }

            return actions;
        }

        private static string GetSelectedFolder()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrWhiteSpace(path))
                return "Assets";
            if (Directory.Exists(ToAbsolutePath(path)))
                return path.Replace('\\', '/');

            string directory = Path.GetDirectoryName(path);
            return string.IsNullOrWhiteSpace(directory) ? "Assets" : directory.Replace('\\', '/');
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static bool IsEditorOnlyPath(string assetPath)
        {
            string[] segments = (assetPath ?? string.Empty).Replace('\\', '/').Split('/');
            foreach (string segment in segments)
            {
                if (string.Equals(segment, "Editor", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string ToIdentifier(string value)
        {
            var builder = new StringBuilder();
            bool capitalize = true;
            foreach (char character in value ?? string.Empty)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    capitalize = true;
                    continue;
                }

                char output = capitalize ? char.ToUpperInvariant(character) : character;
                if (builder.Length == 0 && char.IsDigit(output))
                    builder.Append('_');
                builder.Append(output);
                capitalize = false;
            }

            return builder.Length == 0 ? "NewMiniTool" : builder.ToString();
        }

        private static string ToSlug(string value)
        {
            var builder = new StringBuilder();
            foreach (char character in value?.Trim().ToLowerInvariant() ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
                else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
                    builder.Append('-');
            }

            return builder.ToString().Trim('-');
        }

        private void RefreshSnapshotTypes()
        {
            Type selected = GetSelectedSnapshotType();
            _snapshotTypes = MiniToolSnapshotContractDiscovery.Find(_debugHostPrefab);
            _snapshotContractPrefab = _debugHostPrefab;
            _snapshotTypeIndex = selected == null ? 0 : Array.IndexOf(_snapshotTypes, selected);
            if (_snapshotTypeIndex < 0)
                _snapshotTypeIndex = 0;
        }

        private Type GetSelectedSnapshotType()
        {
            if (_snapshotTypes == null || _snapshotTypes.Length == 0)
                return null;

            return _snapshotTypes[Mathf.Clamp(_snapshotTypeIndex, 0, _snapshotTypes.Length - 1)];
        }

        private static bool IsPrefabAsset(GameObject prefab)
        {
            if (prefab == null)
                return false;

            string path = AssetDatabase.GetAssetPath(prefab);
            return !string.IsNullOrWhiteSpace(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }
    }
}

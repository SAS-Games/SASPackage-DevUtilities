using System;
using System.IO;
using System.Text;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Registry
{
    internal sealed class MiniToolCreationWindow : EditorWindow
    {
        private string _toolName = "New Mini Tool";
        private MonoScript _existingProvider;
        private bool _generateProvider = true;
        private ConsoleCommand _command;
        private RemoteCommandRouting _routing =
            RemoteCommandRouting.ControlEditorToolOnly;
        private GameObject _debugHostPrefab;
        private float _updateInterval = 1f;
        private bool _visibleByDefault = true;

        [MenuItem("Assets/Create/Dev Utilities/Mini Tool Setup...", priority = 120)]
        [MenuItem("Tools/Dev Utilities/Create Mini Tool...", priority = 120)]
        private static void OpenFromMenu()
        {
            OpenWindow();
        }

        internal static void OpenWindow()
        {
            var window = GetWindow<MiniToolCreationWindow>(true, "Create Mini Tool", true);
            window.minSize = new Vector2(440f, 330f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Mini Tool Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Creates the single definition used by the Player, Native Workspace, Debug Host, and command routing.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8f);

            _toolName = EditorGUILayout.TextField("Tool Name", _toolName);
            _generateProvider = EditorGUILayout.ToggleLeft("Generate a Native Workspace field provider", _generateProvider);
            using (new EditorGUI.DisabledScope(_generateProvider))
            {
                _existingProvider = (MonoScript)EditorGUILayout.ObjectField("Existing Data Provider", _existingProvider, typeof(MonoScript), false);
            }

            _updateInterval = EditorGUILayout.FloatField("Update Interval", _updateInterval);
            _command = (ConsoleCommand)EditorGUILayout.ObjectField("Command", _command, typeof(ConsoleCommand), false);
            using (new EditorGUI.DisabledScope(_command == null))
            {
                _routing = (RemoteCommandRouting)EditorGUILayout.EnumPopup("When Command Runs", _routing);
            }

            _debugHostPrefab = (GameObject)EditorGUILayout.ObjectField("Debug Host Prefab", _debugHostPrefab, typeof(GameObject), false);
            _visibleByDefault = EditorGUILayout.Toggle("Visible by Default", _visibleByDefault);

            EditorGUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(!CanCreate()))
            {
                if (GUILayout.Button("Create Mini Tool", GUILayout.Height(28f)))
                    CreateMiniTool();
            }
        }

        private bool CanCreate()
        {
            if (string.IsNullOrWhiteSpace(_toolName))
                return false;
            if (_generateProvider)
                return true;

            Type providerType = _existingProvider?.GetClass();
            return IsProvider(providerType);
        }

        private void CreateMiniTool()
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
                File.WriteAllText(ToAbsolutePath(scriptPath), CreateProviderSource(className), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(scriptPath);
                providerScriptGuid =
                    AssetDatabase.AssetPathToGUID(scriptPath);
                string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath);
                if (string.IsNullOrWhiteSpace(assemblyName))
                    assemblyName = "Assembly-CSharp";
                else
                    assemblyName =
                        Path.GetFileNameWithoutExtension(assemblyName);
                providerTypeName = $"{className}, {assemblyName}";
            }
            else
            {
                Type providerType = _existingProvider.GetClass();
                providerTypeName = $"{providerType.FullName}, {providerType.Assembly.GetName().Name}";
                providerScriptGuid =
                    AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(_existingProvider));
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
            serialized.FindProperty("_providerScriptGuid").stringValue =
                providerScriptGuid;
            serialized.FindProperty("_providerTypeName").stringValue = providerTypeName;
            serialized.FindProperty("_command").objectReferenceValue = _command;
            serialized.FindProperty("_commandName").stringValue = GetDefaultCommandAction(_command);
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
            return type != null &&
                   type.IsClass &&
                   !type.IsAbstract &&
                   typeof(IMiniToolDataProvider).IsAssignableFrom(type);
        }

        private static string GetDefaultCommandAction(ConsoleCommand command)
        {
            if (command == null)
                return string.Empty;

            var serializedCommand = new SerializedObject(command);
            SerializedProperty subCommands = serializedCommand.FindProperty("m_SubCommands");
            if (subCommands == null || !subCommands.isArray || subCommands.arraySize == 0)
                return command.Name;

            string subCommand = subCommands.GetArrayElementAtIndex(0).FindPropertyRelative("Name")?.stringValue;
            return string.IsNullOrWhiteSpace(subCommand) ? command.Name : $"{command.Name}.{subCommand}";
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

        private static string CreateProviderSource(string className)
        {
            return
$@"using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

[UnityEngine.Scripting.Preserve]
public sealed class {className} : MiniToolFieldDataProvider
{{
    public override RemoteMiniToolField[] CaptureFields()
    {{
        return new[]
        {{
            new RemoteMiniToolField
            {{
                Name = ""status"",
                DisplayName = ""Status"",
                Value = ""Running""
            }}
        }};
    }}
}}
";
        }
    }
}

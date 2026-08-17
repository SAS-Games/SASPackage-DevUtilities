using System;
using HP.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.MiniTools.Scaffolding
{
    [Serializable]
    internal sealed class MiniToolScaffoldForm
    {
        [SerializeField] private string _toolName = "New Mini Tool";
        [SerializeField] private string _namespace = "Game.MiniTools";
        [SerializeField] private string _description = string.Empty;
        [SerializeField] private DefaultAsset _outputFolder;
        [SerializeField] private bool _createSubfolder = true;
        [SerializeField] private bool _createCommand = true;
        [SerializeField] private float _updateInterval = 0.5f;
        [SerializeField] private bool _visibleByDefault = true;
        [SerializeField] private RemoteCommandRouting _commandRouting = RemoteCommandRouting.ControlEditorToolOnly;

        internal void InitializeFromSelection()
        {
            if (_outputFolder != null)
                return;

            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrWhiteSpace(selectedPath) && AssetDatabase.IsValidFolder(selectedPath) && selectedPath.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                _outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(selectedPath);
            else
                _outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
        }

        internal bool Draw()
        {
            EditorGUILayout.LabelField("Create New Mini Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Generates a shared snapshot, collector, local Player prefab UI, remote provider, and registration asset.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8f);

            _toolName = EditorGUILayout.TextField("Tool Name", _toolName);
            _namespace = EditorGUILayout.TextField("Namespace", _namespace);
            EditorGUILayout.LabelField("Description");
            _description = EditorGUILayout.TextArea(_description, GUILayout.MinHeight(44f));
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);
            _createSubfolder = EditorGUILayout.Toggle("Create Tool Subfolder", _createSubfolder);
            _updateInterval = EditorGUILayout.FloatField("Update Interval", _updateInterval);
            _visibleByDefault = EditorGUILayout.Toggle("Visible By Default", _visibleByDefault);
            _createCommand = EditorGUILayout.Toggle("Create On / Off Command", _createCommand);
            if (_createCommand)
                _commandRouting = (RemoteCommandRouting)EditorGUILayout.EnumPopup("When Command Runs", _commandRouting);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox("The generated collector returns useful placeholder state and is shared by local and remote providers. Replace its Capture method with your tool's real data collection.", MessageType.Info);
            if (MiniToolScaffoldPersistence.HasPending)
                EditorGUILayout.HelpBox("A mini-tool scaffold is waiting for generated scripts to compile. Use Tools > Dev Utilities > Complete Pending Mini Tool after resolving compilation errors.", MessageType.Warning);

            MiniToolScaffoldRequest request = CreateRequest();
            bool valid = request.TryValidate(out string validationError) && !MiniToolScaffoldPersistence.HasPending;
            if (!string.IsNullOrWhiteSpace(validationError))
                EditorGUILayout.HelpBox(validationError, MessageType.Error);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (!GUILayout.Button("Create Mini Tool", GUILayout.Height(30f)))
                    return false;
            }

            if (!MiniToolScaffoldGenerator.TryBegin(request, out string error))
            {
                EditorUtility.DisplayDialog("Mini Tool Creation Failed", error, "OK");
                return false;
            }

            EditorUtility.DisplayDialog("Mini Tool Scripts Created", "Unity will compile the generated scripts, then automatically create the prefab, command asset, and mini-tool registration.", "OK");
            return true;
        }

        private MiniToolScaffoldRequest CreateRequest()
        {
            return new MiniToolScaffoldRequest
            {
                ToolName = _toolName,
                Namespace = _namespace,
                Description = _description,
                OutputFolder = _outputFolder != null ? AssetDatabase.GetAssetPath(_outputFolder) : string.Empty,
                CreateSubfolder = _createSubfolder,
                CreateCommand = _createCommand,
                UpdateInterval = _updateInterval,
                VisibleByDefault = _visibleByDefault,
                CommandRouting = _commandRouting
            };
        }
    }
}

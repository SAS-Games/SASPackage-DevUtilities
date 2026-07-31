using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI
{
    public sealed class RemoteDevUtilitiesWindow : EditorWindow
    {
        private enum Tab
        {
            Commands,
            Logs,
            MiniTools,
            RuntimeSceneInspector
        }

        private RemoteDevUtilitiesClient _client;
        private RemoteConnectionPanel _connectionPanel;
        private EditorDebugWorkspacePanel _debugWorkspacePanel;
        private RemoteCommandPanel _commandPanel;
        private RemoteLogPanel _logPanel;
        private RemoteMiniToolsPanel _miniToolsPanel;
        private RemoteRuntimeSceneInspectorPanel _runtimeSceneInspectorPanel;
        private Tab _tab;
        private string _initializationError;
        [SerializeField] private bool _showNativeWorkspace;
        [SerializeField] private Vector2 _windowScroll;

        [MenuItem("Tools/Dev Utilities/Remote Dev Utilities")]
        public static void Open()
        {
            var window = GetWindow<RemoteDevUtilitiesWindow>();
            window.titleContent = new GUIContent("Remote Dev Utilities");
            window.minSize = new Vector2(720f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _connectionPanel = new RemoteConnectionPanel();
            _debugWorkspacePanel = new EditorDebugWorkspacePanel();
            _commandPanel = new RemoteCommandPanel();
            _logPanel = new RemoteLogPanel();
            _miniToolsPanel = new RemoteMiniToolsPanel();
            _runtimeSceneInspectorPanel = new RemoteRuntimeSceneInspectorPanel();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RemoteMiniToolVisibilitySettings.Changed += Repaint;
            TryInitializeClient();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            RemoteMiniToolVisibilitySettings.Changed -= Repaint;
            if (_client != null)
            {
                _client.StateChanged -= Repaint;
                _client = null;
            }
        }

        private void OnGUI()
        {
            _windowScroll = EditorGUILayout.BeginScrollView(_windowScroll);
            DrawWindowContent();
            EditorGUILayout.EndScrollView();
        }

        private void DrawWindowContent()
        {
            if (_client == null)
            {
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(_initializationError) ? "Remote Dev Utilities has not initialized." : _initializationError, MessageType.Error);
                if (GUILayout.Button("Retry Initialization", GUILayout.Width(140f)))
                    TryInitializeClient();
                return;
            }

            _connectionPanel.Draw(_client);
            _showNativeWorkspace = _debugWorkspacePanel.Draw(_client, _showNativeWorkspace);
            EditorGUILayout.Space(4f);

            if (!_showNativeWorkspace)
                return;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Native Workspace", EditorStyles.boldLabel);
            _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Commands", "Logs", "Mini Tools", "Runtime Scene Inspector" });
            EditorGUILayout.Space(3f);

            switch (_tab)
            {
                case Tab.Commands:
                    _commandPanel.Draw(_client.Commands, _client.CommandPresentation, _client.IsConnected);
                    break;
                case Tab.Logs:
                    if (_logPanel.Draw(_client.Logs, _client.Commands, _client.IsConnected))
                    {
                        _windowScroll.y = float.MaxValue;
                    }

                    break;
                case Tab.MiniTools:
                    _miniToolsPanel.Draw(_client.MiniTools, _client.IsConnected);
                    break;
                case Tab.RuntimeSceneInspector:
                    _runtimeSceneInspectorPanel.Draw(_client.RuntimeSceneInspector, _client.IsConnected, position);
                    break;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state) => Repaint();

        private void TryInitializeClient()
        {
            if (_client != null)
                return;

            try
            {
                _client = RemoteDevUtilitiesEditorService.Client;
                _client.StateChanged += Repaint;
                _initializationError = null;
            }
            catch (Exception exception)
            {
                _client = null;
                _initializationError = "Remote Dev Utilities failed to initialize:\n" + exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
        }
    }
}

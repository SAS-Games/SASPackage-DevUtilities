using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.UI
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteWorkspacePanelAttribute : Attribute
    {
        public RemoteWorkspacePanelAttribute(string id, string displayName, int order)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A workspace panel id is required.", nameof(id))
                : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            Order = order;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
    }

    internal interface IRemoteWorkspacePanel : IDisposable
    {
        void Initialize(Action repaint);
        bool Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect);
        void Deactivate();
    }

    internal sealed class RemoteWorkspacePanelInstance
    {
        public RemoteWorkspacePanelInstance(RemoteWorkspacePanelAttribute registration, IRemoteWorkspacePanel panel)
        {
            Registration = registration;
            Panel = panel;
        }

        public RemoteWorkspacePanelAttribute Registration { get; }
        public IRemoteWorkspacePanel Panel { get; }
    }

    internal static class RemoteWorkspacePanelRegistry
    {
        internal static IReadOnlyList<RemoteWorkspacePanelInstance> CreatePanels(Action repaint)
        {
            var registrations = new List<(RemoteWorkspacePanelAttribute Attribute, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteWorkspacePanelAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteWorkspacePanel).IsAssignableFrom(type))
                    continue;

                RemoteWorkspacePanelAttribute attribute =
                    (RemoteWorkspacePanelAttribute)Attribute.GetCustomAttribute(type, typeof(RemoteWorkspacePanelAttribute));
                if (attribute != null)
                    registrations.Add((attribute, type));
            }

            registrations.Sort(Compare);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var panels = new List<RemoteWorkspacePanelInstance>(registrations.Count);
            foreach ((RemoteWorkspacePanelAttribute attribute, Type type) in registrations)
            {
                if (!ids.Add(attribute.Id))
                    throw new InvalidOperationException($"A remote workspace panel with id '{attribute.Id}' is already registered.");

                if (Activator.CreateInstance(type, true) is not IRemoteWorkspacePanel panel)
                    throw new InvalidOperationException($"Remote workspace panel '{type.FullName}' could not be created.");
                panel.Initialize(repaint);
                panels.Add(new RemoteWorkspacePanelInstance(attribute, panel));
            }

            return panels;
        }

        private static int Compare(
            (RemoteWorkspacePanelAttribute Attribute, Type Type) left,
            (RemoteWorkspacePanelAttribute Attribute, Type Type) right)
        {
            int order = left.Attribute.Order.CompareTo(right.Attribute.Order);
            return order != 0
                ? order
                : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class RemoteWorkspaceHeaderAttribute : Attribute
    {
        public RemoteWorkspaceHeaderAttribute(int order) => Order = order;
        public int Order { get; }
    }

    internal interface IRemoteWorkspaceHeader
    {
        bool Draw(RemoteDevUtilitiesClient client, bool showNativeWorkspace);
    }

    internal static class RemoteWorkspaceHeaderRegistry
    {
        internal static IReadOnlyList<IRemoteWorkspaceHeader> CreateHeaders()
        {
            var registrations = new List<(int Order, Type Type)>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<RemoteWorkspaceHeaderAttribute>())
            {
                if (type == null || type.IsAbstract || !typeof(IRemoteWorkspaceHeader).IsAssignableFrom(type))
                    continue;

                var attribute = (RemoteWorkspaceHeaderAttribute)Attribute.GetCustomAttribute(
                    type, typeof(RemoteWorkspaceHeaderAttribute));
                if (attribute != null)
                    registrations.Add((attribute.Order, type));
            }

            registrations.Sort((left, right) =>
            {
                int order = left.Order.CompareTo(right.Order);
                return order != 0
                    ? order
                    : string.Compare(left.Type.FullName, right.Type.FullName, StringComparison.Ordinal);
            });

            var headers = new List<IRemoteWorkspaceHeader>(registrations.Count);
            foreach ((int _, Type type) in registrations)
            {
                if (Activator.CreateInstance(type, true) is IRemoteWorkspaceHeader header)
                    headers.Add(header);
            }

            return headers;
        }
    }

    public sealed class RemoteDevUtilitiesWindow : EditorWindow
    {
        private RemoteDevUtilitiesClient _client;
        private RemoteConnectionPanel _connectionPanel;
        private IReadOnlyList<IRemoteWorkspaceHeader> _workspaceHeaders =
            Array.Empty<IRemoteWorkspaceHeader>();
        private IReadOnlyList<RemoteWorkspacePanelInstance> _workspacePanels =
            Array.Empty<RemoteWorkspacePanelInstance>();
        private string _initializationError;
        [SerializeField] private bool _showNativeWorkspace;
        [SerializeField] private string _selectedWorkspacePanelId = "commands";
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
            _connectionPanel = new RemoteConnectionPanel(this, Repaint);
            _workspaceHeaders = RemoteWorkspaceHeaderRegistry.CreateHeaders();
            if (_workspaceHeaders.Count == 0)
                _showNativeWorkspace = true;
            _workspacePanels = RemoteWorkspacePanelRegistry.CreatePanels(Repaint);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            TryInitializeClient();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _connectionPanel?.Dispose();
            _connectionPanel = null;
            DisposeWorkspacePanels();
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
            bool previouslyShowingNativeWorkspace = _showNativeWorkspace;
            for (int i = 0; i < _workspaceHeaders.Count; i++)
                _showNativeWorkspace = _workspaceHeaders[i].Draw(_client, _showNativeWorkspace);
            if (previouslyShowingNativeWorkspace && !_showNativeWorkspace)
                DeactivateSelectedWorkspacePanel();
            EditorGUILayout.Space(4f);

            if (!_showNativeWorkspace)
                return;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("Native Workspace", EditorStyles.boldLabel);
            if (_workspacePanels.Count == 0)
            {
                EditorGUILayout.HelpBox("No Remote Dev Utilities workspace features are installed.", MessageType.Info);
                return;
            }

            int selectedIndex = FindSelectedWorkspacePanelIndex();
            var labels = new string[_workspacePanels.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = _workspacePanels[i].Registration.DisplayName;

            int nextIndex = GUILayout.Toolbar(selectedIndex, labels);
            if (nextIndex != selectedIndex)
            {
                _workspacePanels[selectedIndex].Panel.Deactivate();
                selectedIndex = nextIndex;
                _selectedWorkspacePanelId = _workspacePanels[selectedIndex].Registration.Id;
            }

            EditorGUILayout.Space(3f);
            if (_workspacePanels[selectedIndex].Panel.Draw(_client, _client.IsConnected, position))
                _windowScroll.y = float.MaxValue;
        }

        private int FindSelectedWorkspacePanelIndex()
        {
            for (int i = 0; i < _workspacePanels.Count; i++)
            {
                if (string.Equals(_workspacePanels[i].Registration.Id, _selectedWorkspacePanelId,
                        StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            _selectedWorkspacePanelId = _workspacePanels[0].Registration.Id;
            return 0;
        }

        private void DeactivateSelectedWorkspacePanel()
        {
            if (_workspacePanels.Count > 0)
                _workspacePanels[FindSelectedWorkspacePanelIndex()].Panel.Deactivate();
        }

        private void DisposeWorkspacePanels()
        {
            for (int i = _workspacePanels.Count - 1; i >= 0; i--)
                _workspacePanels[i].Panel.Dispose();
            _workspacePanels = Array.Empty<RemoteWorkspacePanelInstance>();
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

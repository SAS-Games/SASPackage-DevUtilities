using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector.Capture;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal interface IRemoteSceneInspectorMode : IDisposable
    {
        string DisplayName { get; }
        void Initialize(Action repaint);
        bool Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect);
        void Deactivate();
    }

    [RemoteWorkspacePanel("runtime-scene-inspector", "Runtime Scene Inspector", 400)]
    internal sealed class RemoteRuntimeSceneInspectorPanel : IRemoteWorkspacePanel
    {
        private readonly RemoteHierarchyView _hierarchy = new();
        private readonly RemoteInspectorView _inspector = new();
        private readonly RemoteSceneCaptureView _capture = new();
        private readonly List<IRemoteSceneInspectorMode> _additionalModes = new();
        private int _observedPickRevision;
        private long _pendingPickedObjectId;
        private int _sessionGeneration = int.MinValue;
        private int _selectedMode;
        private int _activeMode = -1;
        private Vector2 _hierarchyScroll;
        private Vector2 _captureScroll;
        private Vector2 _inspectorScroll;

        public void Initialize(Action repaint)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IRemoteSceneInspectorMode>())
            {
                if (type == null || type.IsAbstract || type == GetType())
                    continue;
                if (Activator.CreateInstance(type, true) is not IRemoteSceneInspectorMode mode)
                    continue;
                mode.Initialize(repaint);
                _additionalModes.Add(mode);
            }
        }

        bool IRemoteWorkspacePanel.Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect)
        {
            DrawModeToolbar();
            SynchronizeMode();
            if (_selectedMode > 0)
                return _additionalModes[_selectedMode - 1].Draw(client, connected, windowRect);
            Draw(client.GetRequiredFeature<RemoteRuntimeSceneInspectorClient>(), connected, windowRect);
            return false;
        }

        public void Deactivate()
        {
            ReleaseCapture();
            if (_activeMode > 0 && _activeMode <= _additionalModes.Count)
                _additionalModes[_activeMode - 1].Deactivate();
        }

        public void Draw(RemoteRuntimeSceneInspectorClient client, bool connected, Rect windowRect)
        {
            if (!connected)
            {
                EditorGUILayout.HelpBox("Connect to a runtime Player to inspect its hierarchy and shader values.", MessageType.Info);
                return;
            }

            if (_sessionGeneration != client.SessionGeneration)
            {
                _sessionGeneration = client.SessionGeneration;
                _observedPickRevision = client.PickRevision;
                _pendingPickedObjectId = 0;
                _hierarchy.SynchronizeSession(client.SessionGeneration);
            }

            if (_observedPickRevision != client.PickRevision)
            {
                _observedPickRevision = client.PickRevision;
                _pendingPickedObjectId = client.LastPickedObjectId;
            }
            if (_pendingPickedObjectId > 0 && _hierarchy.SelectAndReveal(_pendingPickedObjectId, client.Hierarchy))
                _pendingPickedObjectId = 0;

            float contentWidth = Mathf.Max(680f, windowRect.width - 24f);
            float hierarchyWidth = Mathf.Clamp(contentWidth * 0.24f, 190f, 300f);
            float captureWidth = Mathf.Clamp(contentWidth * 0.4f, 320f, 720f);
            float columnHeight = Mathf.Max(300f, windowRect.height - 230f);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(hierarchyWidth),
                GUILayout.Height(columnHeight));
            _hierarchyScroll = EditorGUILayout.BeginScrollView(_hierarchyScroll);
            _hierarchy.Draw(client);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(captureWidth),
                GUILayout.Height(columnHeight));
            _captureScroll = EditorGUILayout.BeginScrollView(_captureScroll);
            _capture.Draw(client, captureWidth);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(columnHeight));
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            _inspector.Draw(client);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        public void Dispose()
        {
            _capture.Dispose();
            for (int i = _additionalModes.Count - 1; i >= 0; i--)
                _additionalModes[i].Dispose();
            _additionalModes.Clear();
        }

        public void ReleaseCapture() => _capture.ReleaseCapture();

        private void DrawModeToolbar()
        {
            if (_additionalModes.Count == 0)
                return;
            var labels = new string[_additionalModes.Count + 1];
            labels[0] = "Live Capture";
            for (int i = 0; i < _additionalModes.Count; i++)
                labels[i + 1] = _additionalModes[i].DisplayName;
            _selectedMode = GUILayout.Toolbar(_selectedMode, labels, EditorStyles.toolbarButton);
        }

        private void SynchronizeMode()
        {
            if (_activeMode == _selectedMode)
                return;
            if (_activeMode == 0)
                ReleaseCapture();
            else if (_activeMode > 0 && _activeMode <= _additionalModes.Count)
                _additionalModes[_activeMode - 1].Deactivate();
            _activeMode = _selectedMode;
        }
    }
}

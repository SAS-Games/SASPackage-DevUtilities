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
        bool IsAvailable { get; }
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
        private readonly RemoteSceneInspectorWorkspaceLayout _layout = new();
        private readonly RemoteSceneInspectorSelectionBreadcrumb _breadcrumb = new();
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
                if (!mode.IsAvailable)
                {
                    mode.Dispose();
                    continue;
                }
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
                _layout.ShowInspector();
            }
            if (_pendingPickedObjectId > 0 && _hierarchy.SelectAndReveal(_pendingPickedObjectId, client.Hierarchy))
                _pendingPickedObjectId = 0;

            _breadcrumb.Draw(client, "LIVE INSPECTOR");
            _layout.Draw(windowRect, 255f,
                "Hierarchy", "Captured Frame", "Inspector",
                (width, _) =>
                {
                    _hierarchy.DrawToolbar(client);
                    _hierarchyScroll = EditorGUILayout.BeginScrollView(_hierarchyScroll);
                    _hierarchy.DrawContents(client, _layout.ShowInspector);
                    EditorGUILayout.EndScrollView();
                },
                (width, _) =>
                {
                    _captureScroll = EditorGUILayout.BeginScrollView(_captureScroll);
                    _capture.Draw(client, width);
                    EditorGUILayout.EndScrollView();
                },
                (_, _) =>
                {
                    _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
                    _inspector.Draw(client);
                    EditorGUILayout.EndScrollView();
                });
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
            labels[0] = "Inspector";
            for (int i = 0; i < _additionalModes.Count; i++)
                labels[i + 1] = _additionalModes[i].DisplayName;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            _selectedMode = GUILayout.Toolbar(
                _selectedMode,
                labels,
                EditorStyles.toolbarButton,
                GUILayout.Width(CalculateModeToolbarWidth(labels)));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static float CalculateModeToolbarWidth(IReadOnlyList<string> labels)
        {
            float width = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                float contentWidth = EditorStyles.toolbarButton
                    .CalcSize(new GUIContent(labels[i])).x + 20f;
                width += Mathf.Max(92f, contentWidth);
            }

            return width;
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

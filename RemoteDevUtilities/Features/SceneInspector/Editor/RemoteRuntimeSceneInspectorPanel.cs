using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector.Capture;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    [RemoteWorkspacePanel("runtime-scene-inspector", "Runtime Scene Inspector", 400)]
    internal sealed class RemoteRuntimeSceneInspectorPanel : IRemoteWorkspacePanel
    {
        private readonly RemoteHierarchyView _hierarchy = new();
        private readonly RemoteInspectorView _inspector = new();
        private readonly RemoteSceneCaptureView _capture = new();
        private int _observedPickRevision;
        private long _pendingPickedObjectId;
        private int _sessionGeneration = int.MinValue;

        public void Initialize(Action repaint)
        {
        }

        bool IRemoteWorkspacePanel.Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect)
        {
            Draw(client.GetRequiredFeature<RemoteRuntimeSceneInspectorClient>(), connected, windowRect);
            return false;
        }

        public void Deactivate() => ReleaseCapture();

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

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(hierarchyWidth));
            _hierarchy.Draw(client);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(captureWidth));
            _capture.Draw(client, captureWidth);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _inspector.Draw(client);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        public void Dispose() => _capture.Dispose();

        public void ReleaseCapture() => _capture.ReleaseCapture();
    }
}

using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteConnectionPanelContribution(RemoteEditorTransportIds.PlayerConnection, 100)]
    internal sealed class PlayerConnectionPanelContribution : IRemoteConnectionPanelContribution
    {
        private IConnectionState _connectionState;
        private Action _repaint;
        private int _selectedIndex;
        private string _selectedTargetName;
        private bool _refreshRequested;

        public void Initialize(EditorWindow owner, Action repaint)
        {
            _repaint = repaint;
#if UNITY_6000_0_OR_NEWER
            _connectionState = PlayerConnectionGUIUtility.GetConnectionState(owner, OnUnityTargetConnected);
#else
            _connectionState = PlayerConnectionGUIUtility.GetAttachToPlayerState(owner, OnUnityTargetConnected);
#endif
        }

        public void Draw(RemoteDevUtilitiesClient client)
        {
            if (!client.HasTransport(RemoteEditorTransportIds.PlayerConnection))
                return;
            if (_refreshRequested)
            {
                _refreshRequested = false;
                client.RefreshConnectedPlayers();
            }

            EditorGUILayout.LabelField("Unity Player Connection", EditorStyles.boldLabel);
            IReadOnlyList<RemoteEditorPlayerDescriptor> players = client.ConnectedPlayers;
            EditorGUILayout.BeginHorizontal();
            if (players.Count > 0)
            {
                var labels = new string[players.Count];
                for (int i = 0; i < players.Count; i++)
                    labels[i] = players[i].Name;
                _selectedIndex = EditorGUILayout.Popup(Mathf.Clamp(_selectedIndex, 0, players.Count - 1), labels);
            }
            else
            {
                EditorGUILayout.LabelField("No Development Players detected", EditorStyles.miniLabel);
            }

            DrawAttachControl();
            if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                client.RefreshConnectedPlayers();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_selectedTargetName))
                EditorGUILayout.LabelField($"Unity selected: {_selectedTargetName}", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(players.Count == 0))
            {
                if (GUILayout.Button("Connect via Unity Player Connection", GUILayout.Height(24f)))
                {
                    RemoteEditorPlayerDescriptor player = players[Mathf.Clamp(_selectedIndex, 0, players.Count - 1)];
                    client.Connect(player.PlayerId);
                }
            }
        }

        public void Dispose()
        {
            _connectionState?.Dispose();
            _connectionState = null;
            _repaint = null;
        }

        private void DrawAttachControl()
        {
            if (_connectionState == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button("Attach via Unity…", GUILayout.Width(132f));
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(132f, EditorGUIUtility.singleLineHeight,
                EditorStyles.popup, GUILayout.Width(132f));
            PlayerConnectionGUI.ConnectionTargetSelectionDropdown(rect, _connectionState, EditorStyles.popup);
        }

        private void OnUnityTargetConnected(string targetName)
        {
            _selectedTargetName = targetName;
            _refreshRequested = true;
            _repaint?.Invoke();
        }
    }
}

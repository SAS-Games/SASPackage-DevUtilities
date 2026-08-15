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
    [RemoteConnectionPanelContribution(RemoteEditorTransportIds.PlayerConnection, "Unity Players", 100)]
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

            EditorGUILayout.LabelField(
                "Connect to a Development Player detected by Unity Player Connection.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
            IReadOnlyList<RemoteEditorPlayerDescriptor> players = client.ConnectedPlayers;
            if (players.Count > 0)
            {
                var labels = new string[players.Count];
                for (int i = 0; i < players.Count; i++)
                    labels[i] = players[i].Name;

                EditorGUILayout.BeginHorizontal();
                _selectedIndex = EditorGUILayout.Popup(
                    "Target",
                    Mathf.Clamp(_selectedIndex, 0, players.Count - 1),
                    labels);
                if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                    client.RefreshConnectedPlayers();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Connect", GUILayout.Width(120f), GUILayout.Height(26f)))
                {
                    RemoteEditorPlayerDescriptor player =
                        players[Mathf.Clamp(_selectedIndex, 0, players.Count - 1)];
                    client.Connect(player.PlayerId);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No Development Players were detected. Start a Development Build, then refresh the list.",
                    MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh Players", GUILayout.Width(120f)))
                    client.RefreshConnectedPlayers();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Player not listed?", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawAttachControl();
            if (GUILayout.Button("Refresh List", GUILayout.Width(90f)))
                client.RefreshConnectedPlayers();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_selectedTargetName))
                EditorGUILayout.LabelField($"Unity attach target: {_selectedTargetName}", EditorStyles.miniLabel);
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
                    GUILayout.Button("Select Unity Target...", GUILayout.Width(160f));
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(160f, EditorGUIUtility.singleLineHeight,
                EditorStyles.popup, GUILayout.Width(160f));
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

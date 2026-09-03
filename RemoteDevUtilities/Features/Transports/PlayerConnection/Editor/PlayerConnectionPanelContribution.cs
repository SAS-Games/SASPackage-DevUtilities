using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteConnectionPanelContribution(RemoteEditorTransportIds.PlayerConnection, "Unity Targets", 100)]
    internal sealed class PlayerConnectionPanelContribution : IRemoteConnectionPanelContribution
    {
        private int _selectedIndex;

        public void Initialize(EditorWindow owner, Action repaint)
        {
        }

        public void Draw(RemoteDevUtilitiesClient client)
        {
            bool hasLocalEditor = client.HasTransport(RemoteEditorTransportIds.LocalEditor);
            bool hasPlayerConnection = client.HasTransport(RemoteEditorTransportIds.PlayerConnection);
            if (!hasLocalEditor && !hasPlayerConnection)
                return;

            EditorGUILayout.LabelField("Inspect this Editor's Play Mode or a Development Player connected through Unity.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            if (hasLocalEditor)
                DrawLocalEditor(client);

            if (!hasPlayerConnection)
                return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Connected Development Players", EditorStyles.miniBoldLabel);
            IReadOnlyList<RemoteEditorPlayerDescriptor> players = client.ConnectedPlayers;
            if (players.Count > 0)
            {
                var labels = new string[players.Count];
                for (int i = 0; i < players.Count; i++)
                    labels[i] = players[i].Name;

                EditorGUILayout.BeginHorizontal();
                _selectedIndex = EditorGUILayout.Popup("Target", Mathf.Clamp(_selectedIndex, 0, players.Count - 1), labels);
                if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                    client.RefreshConnectedPlayers();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Connect", GUILayout.Width(120f), GUILayout.Height(26f)))
                {
                    RemoteEditorPlayerDescriptor player = players[Mathf.Clamp(_selectedIndex, 0, players.Count - 1)];
                    client.Connect(player.PlayerId);
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("No connected Development Players.", EditorStyles.miniLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(68f)))
                    client.RefreshConnectedPlayers();
                EditorGUILayout.EndHorizontal();
            }
        }

        public void Dispose()
        {
        }

        private static void DrawLocalEditor(RemoteDevUtilitiesClient client)
        {
            bool isPlaying = EditorApplication.isPlaying;
            bool agentEnabled = RemoteDevUtilitiesProjectSettings.instance.Runtime.EnableRemoteAgent;
            bool isAvailable = client.IsLocalEditorAvailable;
            string status;
            if (isAvailable)
                status = "Ready";
            else if (!agentEnabled)
                status = "Agent Disabled";
            else if (!isPlaying)
                status = "Enter Play Mode";
            else
                status = "Starting";

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("This Editor (Play Mode)", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
                }

                using (new EditorGUI.DisabledScope(!isAvailable))
                {
                    if (GUILayout.Button("Connect", GUILayout.Width(90f), GUILayout.Height(24f)))
                        client.ConnectLocalEditor();
                }
            }
        }
    }
}

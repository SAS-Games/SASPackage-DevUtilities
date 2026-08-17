using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteConnectionPanelContribution("lan-discovery", "LAN Players", 300)]
    internal sealed class LanDiscoveryPanelContribution : IRemoteConnectionPanelContribution
    {
        private const string TokenSessionKey = "RemoteDevUtilities.AccessToken";
        private int _selectedIndex;
        private string _accessToken;

        public void Initialize(EditorWindow owner, Action repaint)
        {
            _accessToken = SessionState.GetString(TokenSessionKey, string.Empty);
        }

        public void Draw(RemoteDevUtilitiesClient client)
        {
            if (!client.HasConnectionService<IRemoteLanDiscoveryService>())
                return;
            EditorGUILayout.LabelField(
                "Connect to Remote Dev Utilities Players discovered on the local network.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
            if (!client.HasTransport(RemoteEditorTransportIds.Tcp))
            {
                EditorGUILayout.HelpBox("LAN targets require the optional TCP transport module.", MessageType.Info);
                return;
            }

            IReadOnlyList<RemoteLanPlayerDescriptor> players = client.LanPlayers;
            if (!string.IsNullOrWhiteSpace(client.LanDiscoveryError))
                EditorGUILayout.HelpBox(client.LanDiscoveryError, MessageType.Error);
            if (players.Count == 0)
            {
                EditorGUILayout.HelpBox("Searching for LAN Players...", MessageType.Info);
                EditorGUILayout.HelpBox(
                    "The Player build must enable LAN discovery, allow TCP connections from other machines, " +
                    "and include a non-empty access token in Project Settings > Dev Utilities > Remote Dev Utilities.",
                    MessageType.None);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Search Again", GUILayout.Width(100f)))
                    client.RefreshLanPlayers();
                EditorGUILayout.EndHorizontal();
                return;
            }

            var labels = new string[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                RemoteLanPlayerDescriptor player = players[i];
                string product = string.IsNullOrWhiteSpace(player.Target?.ProductName) ? "Unity Player" : player.Target.ProductName;
                labels[i] = $"{product} - {player.Host}:{player.Port}";
            }
            _selectedIndex = EditorGUILayout.Popup("Target", Mathf.Clamp(_selectedIndex, 0, players.Count - 1), labels);
            EditorGUI.BeginChangeCheck();
            _accessToken = EditorGUILayout.PasswordField("Access Token", _accessToken);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetString(TokenSessionKey, _accessToken ?? string.Empty);

            RemoteLanPlayerDescriptor selected = players[Mathf.Clamp(_selectedIndex, 0, players.Count - 1)];
            if (!selected.IsProtocolCompatible)
                EditorGUILayout.HelpBox("The selected Player uses an incompatible Remote Dev Utilities protocol.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!selected.IsProtocolCompatible || string.IsNullOrWhiteSpace(_accessToken)))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Connect", GUILayout.Width(120f), GUILayout.Height(26f)))
                    client.ConnectTcp(selected.Host, selected.Port, _accessToken);
                EditorGUILayout.EndHorizontal();
            }
        }

        public void Dispose()
        {
        }
    }
}

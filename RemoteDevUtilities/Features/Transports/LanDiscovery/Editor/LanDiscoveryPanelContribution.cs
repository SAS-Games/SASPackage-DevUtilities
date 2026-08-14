using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteConnectionPanelContribution("lan-discovery", 300)]
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
            EditorGUILayout.LabelField("LAN Discovery", EditorStyles.boldLabel);
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
                EditorGUILayout.LabelField("Searching for LAN Players…", EditorStyles.miniLabel);
                return;
            }

            var labels = new string[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                RemoteLanPlayerDescriptor player = players[i];
                string product = string.IsNullOrWhiteSpace(player.Target?.ProductName) ? "Unity Player" : player.Target.ProductName;
                labels[i] = $"{product} — {player.Host}:{player.Port}";
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
                if (GUILayout.Button("Connect to LAN Player", GUILayout.Height(24f)))
                    client.ConnectTcp(selected.Host, selected.Port, _accessToken);
            }
        }

        public void Dispose()
        {
        }
    }
}

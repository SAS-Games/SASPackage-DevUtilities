using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    internal sealed class RemoteConnectionPanel
    {
        private enum ConnectionMode
        {
            DiscoveredPlayer,
            DirectIp
        }

        private const string ModePreferenceKey = "RemoteDevUtilities.ConnectionMode";
        private const string HostPreferenceKey = "RemoteDevUtilities.TcpHost";
        private const string PortPreferenceKey = "RemoteDevUtilities.TcpPort";
        private const string TokenSessionKey = "RemoteDevUtilities.AccessToken";
        private const string ExpandedPreferenceKey = "RemoteDevUtilities.ConnectionPanelExpanded";

        private int? _selectedPlayerId;
        private ConnectionMode _mode;
        private string _tcpHost;
        private int _tcpPort;
        private string _accessToken;
        private bool _expanded;

        private RemoteDevUtilitiesRuntimeConfiguration RuntimeSettings => RemoteDevUtilitiesProjectSettings.instance.Runtime;
        private int ConfiguredTcpPort => RuntimeSettings?.TcpPort ?? RemoteProtocolConstants.DefaultTcpPort;

        public RemoteConnectionPanel()
        {
            _mode = (ConnectionMode)Mathf.Clamp(EditorPrefs.GetInt(ModePreferenceKey, (int)ConnectionMode.DiscoveredPlayer), 0, 1);
            _tcpHost = EditorPrefs.GetString(HostPreferenceKey, "127.0.0.1");
            _tcpPort = EditorPrefs.GetInt(PortPreferenceKey, ConfiguredTcpPort);
            _accessToken = SessionState.GetString(TokenSessionKey, string.Empty);
            _expanded = EditorPrefs.GetBool(ExpandedPreferenceKey, true);
        }

        public void Draw(RemoteDevUtilitiesClient client)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool expanded = EditorGUILayout.Foldout(_expanded, BuildHeader(client), true, EditorStyles.foldoutHeader);
            if (expanded != _expanded)
            {
                _expanded = expanded;
                EditorPrefs.SetBool(ExpandedPreferenceKey, _expanded);
            }

            if (!_expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            using (new EditorGUI.DisabledScope(client.IsConnected || client.IsHandshakePending))
            {
                DrawConnectionMode();
                EditorGUILayout.Space(2f);
                if (_mode == ConnectionMode.DiscoveredPlayer)
                    DrawPlayerTarget(client);
                else
                {
                    DrawTcpTarget();
                    DrawAccessToken();
                }
            }

            EditorGUILayout.Space(3f);
            DrawConnectionAction(client);
            DrawConnectionStatus(client);
            EditorGUILayout.EndVertical();
        }

        private static string BuildHeader(RemoteDevUtilitiesClient client)
        {
            if (client.IsConnected)
            {
                string target = string.IsNullOrWhiteSpace(client.SelectedTargetName) ? "Connected" : client.SelectedTargetName;
                return $"Remote Target — {target}";
            }

            if (client.IsHandshakePending)
                return "Remote Target — Connecting";

            return "Remote Target";
        }

        private void DrawConnectionMode()
        {
            ConnectionMode nextMode = (ConnectionMode)GUILayout.Toolbar((int)_mode, new[] { "Discovered Players", "Direct IP" });
            if (nextMode == _mode)
                return;

            SetConnectionMode(nextMode);
        }

        private void DrawPlayerTarget(RemoteDevUtilitiesClient client)
        {
            IReadOnlyList<RemoteEditorPlayerDescriptor> players = client.ConnectedPlayers;
            string[] labels = new string[players.Count];
            int selectedIndex = -1;
            int? activePlayerId = client.ConnectionKind == RemoteEditorConnectionKind.PlayerConnection ? client.SelectedPlayerId : _selectedPlayerId;
            for (int i = 0; i < players.Count; i++)
            {
                RemoteEditorPlayerDescriptor player = players[i];
                labels[i] = $"{player.Name}  [{player.PlayerId}]";
                if (activePlayerId.HasValue && player.PlayerId == activePlayerId.Value)
                    selectedIndex = i;
            }

            if (selectedIndex < 0 && players.Count > 0)
                selectedIndex = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Player", GUILayout.Width(90f));
            int nextIndex = EditorGUILayout.Popup(Mathf.Max(0, selectedIndex), labels.Length == 0 ? new[] { "No Unity-discovered Players" } : labels);
            if (GUILayout.Button(new GUIContent("Refresh", "Reload the Players currently known to Unity PlayerConnection."), GUILayout.Width(68f)))
            {
                client.RefreshConnectedPlayers();
            }

            EditorGUILayout.EndHorizontal();

            if (labels.Length == 0)
            {
                _selectedPlayerId = null;
                EditorGUILayout.HelpBox("Unity only lists Players that have established a PlayerConnection. " + "If Autoconnect Profiler is disabled, use Direct IP instead.", MessageType.Info);
                if (GUILayout.Button("Use Direct IP", GUILayout.Width(110f)))
                {
                    SetConnectionMode(ConnectionMode.DirectIp);
                }

                return;
            }

            _selectedPlayerId = players[Mathf.Clamp(nextIndex, 0, players.Count - 1)].PlayerId;
        }

        private void DrawTcpTarget()
        {
            RemoteDevUtilitiesRuntimeConfiguration runtimeSettings = RuntimeSettings;
            int configuredTcpPort = ConfiguredTcpPort;

            EditorGUI.BeginChangeCheck();
            _tcpHost = EditorGUILayout.TextField("Host", _tcpHost);
            _tcpPort = EditorGUILayout.IntField("Port", _tcpPort);
            _tcpPort = Mathf.Clamp(_tcpPort, 1, 65535);
            if (EditorGUI.EndChangeCheck())
                PersistTcpTarget();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(94f);
            if (GUILayout.Button("Use Localhost", GUILayout.Width(100f)))
            {
                _tcpHost = "127.0.0.1";
                PersistTcpTarget();
            }

            if (GUILayout.Button($"Use Port {configuredTcpPort}", GUILayout.Width(112f)))
            {
                _tcpPort = configuredTcpPort;
                PersistTcpTarget();
            }

            if (GUILayout.Button("Open Settings", GUILayout.Width(100f)))
            {
                RemoteDevUtilitiesProjectSettings.Open();
            }

            EditorGUILayout.EndHorizontal();

            string listenerScope = runtimeSettings != null && runtimeSettings.AllowTcpConnectionsFromOtherMachines ? "local network" : "this machine only";
            EditorGUILayout.LabelField($"The current build settings use TCP port {configuredTcpPort} " + $"and allow connections from {listenerScope}.", EditorStyles.wordWrappedMiniLabel);

            if (!IsLoopbackHost(_tcpHost) && (runtimeSettings == null || !runtimeSettings.AllowTcpConnectionsFromOtherMachines))
            {
                EditorGUILayout.HelpBox("The runtime settings are loopback-only. Enable " + "\"Allow Tcp Connections From Other Machines\" before " + "building a Player for another PC.", MessageType.Warning);
            }
            else if (!IsLoopbackHost(_tcpHost) && string.IsNullOrWhiteSpace(runtimeSettings?.TcpAccessToken))
            {
                EditorGUILayout.HelpBox("Connections from another machine require a non-empty " + "access token in the runtime settings.", MessageType.Warning);
            }
        }

        private void DrawAccessToken()
        {
            EditorGUI.BeginChangeCheck();
            _accessToken = EditorGUILayout.PasswordField(new GUIContent("Access Token", "Must match RemoteDevUtilitiesSettings. Leave empty for the default loopback-only configuration."), _accessToken);
            if (EditorGUI.EndChangeCheck())
                SessionState.SetString(TokenSessionKey, _accessToken ?? string.Empty);
        }

        private void DrawConnectionAction(RemoteDevUtilitiesClient client)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (client.IsConnected)
            {
                EditorGUILayout.LabelField($"Connected: {client.SelectedTargetName}", EditorStyles.miniLabel);
                if (GUILayout.Button("Disconnect", GUILayout.Width(90f)))
                    client.Disconnect();
            }
            else if (client.IsHandshakePending)
            {
                EditorGUILayout.LabelField($"Connecting: {client.SelectedTargetName}", EditorStyles.miniLabel);
                if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
                    client.CancelConnect();
            }
            else
            {
                bool canConnect = _mode == ConnectionMode.DiscoveredPlayer ? _selectedPlayerId.HasValue : !string.IsNullOrWhiteSpace(_tcpHost) && _tcpPort > 0 && _tcpPort <= 65535;
                using (new EditorGUI.DisabledScope(!canConnect))
                {
                    if (GUILayout.Button("Connect", GUILayout.Width(90f)))
                    {
                        if (_mode == ConnectionMode.DiscoveredPlayer)
                            client.Connect(_selectedPlayerId.Value);
                        else
                            client.ConnectTcp(_tcpHost, _tcpPort, _accessToken);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnectionStatus(RemoteDevUtilitiesClient client)
        {
            if (!string.IsNullOrEmpty(client.ConnectionError))
                EditorGUILayout.HelpBox(client.ConnectionError, MessageType.Error);
            else if (client.IsConnected && client.Target != null)
                DrawTarget(client);
            else if (client.IsHandshakePending)
                EditorGUILayout.LabelField("Waiting for the runtime agent handshake...", EditorStyles.miniLabel);
            else if (_mode == ConnectionMode.DirectIp)
                EditorGUILayout.LabelField("Direct IP works with Development and release Players built " + "with ENABLE_DEBUG. Use 127.0.0.1 on the same machine, or " + "the Player machine's IP address on a trusted network.", EditorStyles.wordWrappedMiniLabel);
            else
                EditorGUILayout.LabelField("Refresh shows Players already connected through Unity. " + "Use Direct IP when Unity discovery is unavailable.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawTarget(RemoteDevUtilitiesClient client)
        {
            string status = $"{client.Target.ProductName} {client.Target.ApplicationVersion}  |  " + $"{client.Target.Platform}  |  Unity {client.Target.UnityVersion}  |  " + $"{client.Target.DeviceName}";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

            if (!client.Target.IsDevUtilitiesEnabled && !client.Target.IsDebugBuild)
                EditorGUILayout.HelpBox("The target does not report complete Dev Utilities support.", MessageType.Warning);
            else if (!client.Target.IsDebugBuild)
                EditorGUILayout.HelpBox("Connected to a non-Development Player with ENABLE_DEBUG.", MessageType.Info);
        }

        private void SetConnectionMode(ConnectionMode mode)
        {
            _mode = mode;
            EditorPrefs.SetInt(ModePreferenceKey, (int)_mode);
        }

        private void PersistTcpTarget()
        {
            EditorPrefs.SetString(HostPreferenceKey, string.IsNullOrWhiteSpace(_tcpHost) ? "127.0.0.1" : _tcpHost.Trim());
            EditorPrefs.SetInt(PortPreferenceKey, _tcpPort);
        }

        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            string value = host.Trim();
            return string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) || value == "127.0.0.1" || value == "::1";
        }
    }
}

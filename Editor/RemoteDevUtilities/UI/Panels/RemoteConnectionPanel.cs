using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Configuration;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    internal sealed class RemoteConnectionPanel
    {
        private enum TargetKind
        {
            None,
            UnityPlayer,
            LanPlayer,
            ManualIp
        }

        private readonly struct TargetOption
        {
            public TargetOption(TargetKind kind, string key, string label, int playerId = -1, string lanSessionId = null)
            {
                Kind = kind;
                Key = key;
                Label = label;
                PlayerId = playerId;
                LanSessionId = lanSessionId;
            }

            public TargetKind Kind { get; }
            public string Key { get; }
            public string Label { get; }
            public int PlayerId { get; }
            public string LanSessionId { get; }
        }

        private const string LegacyModePreferenceKey = "RemoteDevUtilities.ConnectionMode";
        private const string HostPreferenceKey = "RemoteDevUtilities.TcpHost";
        private const string PortPreferenceKey = "RemoteDevUtilities.TcpPort";
        private const string TokenSessionKey = "RemoteDevUtilities.AccessToken";
        private const string ExpandedPreferenceKey = "RemoteDevUtilities.ConnectionPanelExpanded";
        private const string ManualTargetKey = "manual";

        private static GUIStyle s_UnityAttachNativeStyle;
        private static GUIStyle s_UnityAttachLabelStyle;

        private TargetKind _selectedTargetKind;
        private string _selectedTargetKey;
        private int? _selectedPlayerId;
        private string _selectedLanSessionId;
        private string _tcpHost;
        private int _tcpPort;
        private string _accessToken;
        private string _lastUnityTargetName;
        private bool _pendingUnityTargetSelection;
        private bool _expanded;

        private RemoteDevUtilitiesRuntimeConfiguration RuntimeSettings => RemoteDevUtilitiesProjectSettings.instance.Runtime;
        private int ConfiguredTcpPort => RuntimeSettings?.TcpPort ?? RemoteProtocolConstants.DefaultTcpPort;

        public RemoteConnectionPanel()
        {
            int legacyMode = Mathf.Clamp(EditorPrefs.GetInt(LegacyModePreferenceKey, 0), 0, 2);
            _selectedTargetKind = legacyMode switch
            {
                1 => TargetKind.ManualIp,
                2 => TargetKind.LanPlayer,
                _ => TargetKind.UnityPlayer
            };
            _tcpHost = EditorPrefs.GetString(HostPreferenceKey, "127.0.0.1");
            _tcpPort = EditorPrefs.GetInt(PortPreferenceKey, ConfiguredTcpPort);
            _accessToken = SessionState.GetString(TokenSessionKey, string.Empty);
            _expanded = EditorPrefs.GetBool(ExpandedPreferenceKey, true);
        }

        public void Draw(RemoteDevUtilitiesClient client, IConnectionState unityConnectionState)
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
                DrawTargetPicker(client, unityConnectionState);
                DrawAutomaticTargetStatus(client);
                DrawSelectedTargetConfiguration(client);
            }

            EditorGUILayout.Space(3f);
            DrawConnectionAction(client);
            DrawConnectionStatus(client);
            EditorGUILayout.EndVertical();
        }

        public void NotifyUnityTargetConnected(string targetName)
        {
            _lastUnityTargetName = string.IsNullOrWhiteSpace(targetName) ? null : targetName;
            _pendingUnityTargetSelection = !string.IsNullOrWhiteSpace(_lastUnityTargetName);
            _selectedTargetKey = null;
            _selectedTargetKind = TargetKind.None;
        }

        private static string BuildHeader(RemoteDevUtilitiesClient client)
        {
            if (client.IsConnected)
            {
                string target = string.IsNullOrWhiteSpace(client.SelectedTargetName) ? "Connected" : client.SelectedTargetName;
                return $"Remote Target — {target}";
            }

            return client.IsHandshakePending ? "Remote Target — Connecting" : "Remote Target";
        }

        private void DrawTargetPicker(RemoteDevUtilitiesClient client, IConnectionState unityConnectionState)
        {
            List<TargetOption> options = BuildTargetOptions(client);
            int selectedIndex = FindSelectedIndex(options);
            if (selectedIndex < 0)
                selectedIndex = FindInitialIndex(options);

            string[] labels = new string[options.Count];
            for (int i = 0; i < options.Count; i++)
                labels[i] = options[i].Label;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target", GUILayout.Width(90f));
            int nextIndex = EditorGUILayout.Popup(selectedIndex, labels);
            DrawUnityAttachControl(unityConnectionState);
            if (GUILayout.Button(new GUIContent("Refresh", "Refresh Unity connections. LAN targets update automatically from discovery beacons."), GUILayout.Width(68f)))
                client.RefreshConnectedPlayers();
            EditorGUILayout.EndHorizontal();

            SelectOption(options[Mathf.Clamp(nextIndex, 0, options.Count - 1)]);
        }

        private List<TargetOption> BuildTargetOptions(RemoteDevUtilitiesClient client)
        {
            var options = new List<TargetOption>();
            IReadOnlyList<RemoteEditorPlayerDescriptor> unityPlayers = client.ConnectedPlayers;
            for (int i = 0; i < unityPlayers.Count; i++)
            {
                RemoteEditorPlayerDescriptor player = unityPlayers[i];
                var option = new TargetOption(TargetKind.UnityPlayer, $"unity:{player.PlayerId}", $"{player.Name}  ·  Unity", player.PlayerId);
                options.Add(option);
                if (_pendingUnityTargetSelection && IsMatchingUnityTarget(player.Name, _lastUnityTargetName))
                {
                    _selectedTargetKey = option.Key;
                    _selectedTargetKind = TargetKind.UnityPlayer;
                    _pendingUnityTargetSelection = false;
                }
            }

            IReadOnlyList<RemoteLanPlayerDescriptor> lanPlayers = client.LanPlayers;
            for (int i = 0; i < lanPlayers.Count; i++)
            {
                RemoteLanPlayerDescriptor player = lanPlayers[i];
                string product = string.IsNullOrWhiteSpace(player.Target?.ProductName) ? "Unity Player" : player.Target.ProductName;
                string device = string.IsNullOrWhiteSpace(player.Target?.DeviceName) ? player.Host : player.Target.DeviceName;
                string compatibility = player.IsProtocolCompatible ? string.Empty : $"  ·  protocol {player.ProtocolVersion}";
                options.Add(new TargetOption(TargetKind.LanPlayer, $"lan:{player.RuntimeSessionId}",
                    $"{product} on {device}  ({player.Host}:{player.Port})  ·  LAN{compatibility}", lanSessionId: player.RuntimeSessionId));
            }

            if (_pendingUnityTargetSelection)
                options.Insert(0, new TargetOption(TargetKind.None, "unity:pending", $"Waiting for {_lastUnityTargetName}…"));

            options.Add(new TargetOption(TargetKind.ManualIp, ManualTargetKey, "Manual IP address…"));
            return options;
        }

        private int FindSelectedIndex(IReadOnlyList<TargetOption> options)
        {
            if (!string.IsNullOrWhiteSpace(_selectedTargetKey))
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (string.Equals(options[i].Key, _selectedTargetKey, StringComparison.Ordinal))
                        return i;
                }
            }

            if (_pendingUnityTargetSelection)
                return 0;

            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Kind == _selectedTargetKind)
                    return i;
            }

            return -1;
        }

        private static int FindInitialIndex(IReadOnlyList<TargetOption> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Kind != TargetKind.None)
                    return i;
            }

            return 0;
        }

        private void SelectOption(TargetOption option)
        {
            _selectedTargetKind = option.Kind;
            _selectedTargetKey = option.Key;
            _selectedPlayerId = option.Kind == TargetKind.UnityPlayer ? option.PlayerId : null;
            _selectedLanSessionId = option.Kind == TargetKind.LanPlayer ? option.LanSessionId : null;

            int legacyMode = option.Kind switch
            {
                TargetKind.ManualIp => 1,
                TargetKind.LanPlayer => 2,
                _ => 0
            };
            EditorPrefs.SetInt(LegacyModePreferenceKey, legacyMode);
        }

        private static void DrawUnityAttachControl(IConnectionState unityConnectionState)
        {
            if (unityConnectionState == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button("Attach via Unity...", GUILayout.Width(132f));
            }
            else
            {
                EnsureUnityAttachStyles();
                Rect rect = GUILayoutUtility.GetRect(132f, EditorGUIUtility.singleLineHeight, s_UnityAttachNativeStyle, GUILayout.Width(132f));
                PlayerConnectionGUI.ConnectionTargetSelectionDropdown(rect, unityConnectionState, s_UnityAttachNativeStyle);
                GUI.Label(rect, new GUIContent("Attach via Unity...",
                    "Opens Unity's native target menu. Choose a standalone Development Player; Play Mode is the Editor, not a remote build."), s_UnityAttachLabelStyle);
            }
        }

        private static void DrawAutomaticTargetStatus(RemoteDevUtilitiesClient client)
        {
            if (client.ConnectedPlayers.Count == 0 && client.LanPlayers.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(client.LanDiscoveryError))
                    EditorGUILayout.HelpBox(client.LanDiscoveryError, MessageType.Error);
                else
                    EditorGUILayout.LabelField("Searching for Dev Utilities Players... Use Attach via Unity for a Development Player, or choose Manual IP.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void EnsureUnityAttachStyles()
        {
            if (s_UnityAttachNativeStyle != null)
                return;

            s_UnityAttachNativeStyle = new GUIStyle(EditorStyles.popup);
            HideStyleText(s_UnityAttachNativeStyle.normal);
            HideStyleText(s_UnityAttachNativeStyle.hover);
            HideStyleText(s_UnityAttachNativeStyle.active);
            HideStyleText(s_UnityAttachNativeStyle.focused);
            HideStyleText(s_UnityAttachNativeStyle.onNormal);
            HideStyleText(s_UnityAttachNativeStyle.onHover);
            HideStyleText(s_UnityAttachNativeStyle.onActive);
            HideStyleText(s_UnityAttachNativeStyle.onFocused);

            s_UnityAttachLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                padding = new RectOffset(
                    EditorStyles.popup.padding.left,
                    EditorStyles.popup.padding.right + 12,
                    EditorStyles.popup.padding.top,
                    EditorStyles.popup.padding.bottom)
            };
        }

        private static void HideStyleText(GUIStyleState state)
        {
            state.textColor = Color.clear;
        }

        private void DrawSelectedTargetConfiguration(RemoteDevUtilitiesClient client)
        {
            switch (_selectedTargetKind)
            {
                case TargetKind.LanPlayer:
                    DrawLanTargetStatus(client);
                    DrawAccessToken();
                    break;
                case TargetKind.ManualIp:
                    DrawTcpTarget();
                    DrawAccessToken();
                    break;
                case TargetKind.None:
                    EditorGUILayout.HelpBox("Waiting for Unity to establish the selected Player connection.", MessageType.Info);
                    break;
            }
        }

        private void DrawLanTargetStatus(RemoteDevUtilitiesClient client)
        {
            RemoteLanPlayerDescriptor selected = FindSelectedLanPlayer(client);
            if (selected == null)
            {
                EditorGUILayout.HelpBox("This LAN target is no longer advertising. Choose another target or wait for it to reappear.", MessageType.Info);
                return;
            }

            if (!selected.IsProtocolCompatible)
            {
                EditorGUILayout.HelpBox($"This Player uses protocol {selected.ProtocolVersion}; this Editor uses " +
                                        $"protocol {RemoteProtocolConstants.Version}. Install matching package versions.", MessageType.Warning);
            }
        }

        private void DrawTcpTarget()
        {
            RemoteDevUtilitiesRuntimeConfiguration runtimeSettings = RuntimeSettings;
            int configuredTcpPort = ConfiguredTcpPort;

            EditorGUI.BeginChangeCheck();
            _tcpHost = EditorGUILayout.TextField("Host", _tcpHost);
            _tcpPort = Mathf.Clamp(EditorGUILayout.IntField("Port", _tcpPort), 1, 65535);
            if (EditorGUI.EndChangeCheck())
                PersistTcpTarget();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(94f);
            if (GUILayout.Button("Localhost", GUILayout.Width(86f)))
            {
                _tcpHost = "127.0.0.1";
                PersistTcpTarget();
            }
            if (GUILayout.Button($"Port {configuredTcpPort}", GUILayout.Width(92f)))
            {
                _tcpPort = configuredTcpPort;
                PersistTcpTarget();
            }
            if (GUILayout.Button("Build Settings", GUILayout.Width(96f)))
                RemoteDevUtilitiesProjectSettings.Open();
            EditorGUILayout.EndHorizontal();

            if (runtimeSettings?.TcpPortFallbackCount > 0)
            {
                int finalPort = Mathf.Min(65535, configuredTcpPort + runtimeSettings.TcpPortFallbackCount);
                if (finalPort > configuredTcpPort)
                    EditorGUILayout.LabelField($"A busy build port can fall back through {finalPort}; Player.log reports the selected port.", EditorStyles.wordWrappedMiniLabel);
            }

            if (!IsLoopbackHost(_tcpHost) && (runtimeSettings == null || !runtimeSettings.AllowTcpConnectionsFromOtherMachines))
                EditorGUILayout.HelpBox("The current build settings accept local connections only.", MessageType.Warning);
            else if (!IsLoopbackHost(_tcpHost) && string.IsNullOrWhiteSpace(runtimeSettings?.TcpAccessToken))
                EditorGUILayout.HelpBox("Remote-machine TCP connections require a build access token.", MessageType.Warning);
        }

        private void DrawAccessToken()
        {
            EditorGUI.BeginChangeCheck();
            _accessToken = EditorGUILayout.PasswordField(new GUIContent("Access Token", "Must match the token baked into the target build."), _accessToken);
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
                RemoteLanPlayerDescriptor lanPlayer = FindSelectedLanPlayer(client);
                bool canConnect = _selectedTargetKind switch
                {
                    TargetKind.UnityPlayer => _selectedPlayerId.HasValue,
                    TargetKind.LanPlayer => lanPlayer?.IsProtocolCompatible == true && !string.IsNullOrWhiteSpace(_accessToken),
                    TargetKind.ManualIp => !string.IsNullOrWhiteSpace(_tcpHost) && _tcpPort > 0 && _tcpPort <= 65535,
                    _ => false
                };
                using (new EditorGUI.DisabledScope(!canConnect))
                {
                    if (GUILayout.Button("Connect", GUILayout.Width(90f)))
                    {
                        switch (_selectedTargetKind)
                        {
                            case TargetKind.UnityPlayer:
                                client.Connect(_selectedPlayerId.Value);
                                break;
                            case TargetKind.LanPlayer:
                                client.ConnectTcp(lanPlayer.Host, lanPlayer.Port, _accessToken);
                                break;
                            case TargetKind.ManualIp:
                                client.ConnectTcp(_tcpHost, _tcpPort, _accessToken);
                                break;
                        }
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
                EditorGUILayout.LabelField("Waiting for the runtime agent handshake…", EditorStyles.miniLabel);
            else if (_selectedTargetKind == TargetKind.LanPlayer)
                EditorGUILayout.LabelField("LAN targets require the access token baked into the selected build.", EditorStyles.wordWrappedMiniLabel);
            else if (_selectedTargetKind == TargetKind.ManualIp)
                EditorGUILayout.LabelField("Use 127.0.0.1 for a Player on this machine, or its network address on a trusted LAN.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawTarget(RemoteDevUtilitiesClient client)
        {
            string status = $"{client.Target.ProductName} {client.Target.ApplicationVersion}  |  " +
                            $"{client.Target.Platform}  |  Unity {client.Target.UnityVersion}  |  {client.Target.DeviceName}";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

            if (!client.Target.IsDevUtilitiesEnabled && !client.Target.IsDebugBuild)
                EditorGUILayout.HelpBox("The target does not report complete Dev Utilities support.", MessageType.Warning);
            else if (!client.Target.IsDebugBuild)
                EditorGUILayout.HelpBox("Connected to a non-Development Player with ENABLE_DEBUG.", MessageType.Info);
        }

        private void PersistTcpTarget()
        {
            EditorPrefs.SetString(HostPreferenceKey, string.IsNullOrWhiteSpace(_tcpHost) ? "127.0.0.1" : _tcpHost.Trim());
            EditorPrefs.SetInt(PortPreferenceKey, _tcpPort);
        }

        private RemoteLanPlayerDescriptor FindSelectedLanPlayer(RemoteDevUtilitiesClient client)
        {
            IReadOnlyList<RemoteLanPlayerDescriptor> players = client.LanPlayers;
            for (int i = 0; i < players.Count; i++)
            {
                if (string.Equals(players[i].RuntimeSessionId, _selectedLanSessionId, StringComparison.Ordinal))
                    return players[i];
            }
            return null;
        }

        private static bool IsMatchingUnityTarget(string connectedPlayerName, string selectedTargetName)
        {
            if (string.IsNullOrWhiteSpace(connectedPlayerName) || string.IsNullOrWhiteSpace(selectedTargetName))
                return false;

            return string.Equals(connectedPlayerName, selectedTargetName, StringComparison.OrdinalIgnoreCase) ||
                   connectedPlayerName.IndexOf(selectedTargetName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   selectedTargetName.IndexOf(connectedPlayerName, StringComparison.OrdinalIgnoreCase) >= 0;
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

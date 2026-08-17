using System;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.Configuration;
using HP.Utilities.RemoteDevUtilities.Editor.UI.Panels;
using HP.Utilities.RemoteDevUtilities.Protocol;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.Connection.Tcp
{
    [RemoteConnectionPanelContribution(RemoteEditorTransportIds.Tcp, "Direct TCP", 200)]
    internal sealed class TcpConnectionPanelContribution : IRemoteConnectionPanelContribution
    {
        private const string HostPreferenceKey = "RemoteDevUtilities.TcpHost";
        private const string PortPreferenceKey = "RemoteDevUtilities.TcpPort";
        private const string TokenSessionKey = "RemoteDevUtilities.AccessToken";
        private string _host;
        private int _port;
        private string _accessToken;

        public void Initialize(EditorWindow owner, Action repaint)
        {
            _host = EditorPrefs.GetString(HostPreferenceKey, "127.0.0.1");
            _port = EditorPrefs.GetInt(PortPreferenceKey, ConfiguredTcpPort);
            _accessToken = SessionState.GetString(TokenSessionKey, string.Empty);
        }

        public void Draw(RemoteDevUtilitiesClient client)
        {
            if (!client.HasTransport(RemoteEditorTransportIds.Tcp))
                return;
            EditorGUILayout.LabelField(
                "Connect directly by host and port. Use this for remote machines or Players not visible through Unity.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            _host = EditorGUILayout.TextField("Host", _host);
            GUILayout.Label("Port", GUILayout.Width(28f));
            _port = Mathf.Clamp(EditorGUILayout.IntField(_port, GUILayout.Width(72f)), 1, 65535);
            EditorGUILayout.EndHorizontal();
            _accessToken = EditorGUILayout.PasswordField("Access Token", _accessToken);
            if (EditorGUI.EndChangeCheck())
                Persist();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Localhost", GUILayout.Width(86f)))
            {
                _host = "127.0.0.1";
                Persist();
            }
            if (GUILayout.Button($"Port {ConfiguredTcpPort}", GUILayout.Width(92f)))
            {
                _port = ConfiguredTcpPort;
                Persist();
            }
            if (GUILayout.Button("Build Settings", GUILayout.Width(96f)))
                RemoteDevUtilitiesProjectSettings.Open();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_host)))
            {
                if (GUILayout.Button("Connect", GUILayout.Width(120f), GUILayout.Height(26f)))
                    client.ConnectTcp(_host, _port, _accessToken);
            }
            EditorGUILayout.EndHorizontal();

            RemoteDevUtilitiesRuntimeConfiguration settings = RemoteDevUtilitiesProjectSettings.instance.Runtime;
            if (!IsLoopbackHost(_host) && (settings == null || !settings.AllowTcpConnectionsFromOtherMachines))
                EditorGUILayout.HelpBox("The current build settings accept local connections only.", MessageType.Warning);
            else if (!IsLoopbackHost(_host) && string.IsNullOrWhiteSpace(settings?.TcpAccessToken))
                EditorGUILayout.HelpBox("Remote-machine TCP connections require a build access token.", MessageType.Warning);
        }

        public void Dispose()
        {
        }

        private static int ConfiguredTcpPort =>
            RemoteDevUtilitiesProjectSettings.instance.Runtime?.TcpPort ?? RemoteProtocolConstants.DefaultTcpPort;

        private void Persist()
        {
            EditorPrefs.SetString(HostPreferenceKey, string.IsNullOrWhiteSpace(_host) ? "127.0.0.1" : _host.Trim());
            EditorPrefs.SetInt(PortPreferenceKey, _port);
            SessionState.SetString(TokenSessionKey, _accessToken ?? string.Empty);
        }

        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;
            string value = host.Trim();
            return string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   value == "127.0.0.1" || value == "::1";
        }
    }
}

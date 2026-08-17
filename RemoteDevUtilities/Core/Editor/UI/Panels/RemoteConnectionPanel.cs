using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.Configuration;
using HP.Utilities.RemoteDevUtilities.Editor.Connection;
using HP.Utilities.RemoteDevUtilities.Protocol.Connection;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    internal sealed class RemoteConnectionPanel : IDisposable
    {
        private const string ExpandedPreferenceKey = "RemoteDevUtilities.ConnectionPanelExpanded";
        private const string SelectedMethodPreferenceKey = "RemoteDevUtilities.ConnectionMethod";
        private readonly IReadOnlyList<RemoteConnectionPanelContributionInstance> _contributions;
        private bool _expanded;
        private string _selectedMethodId;

        public RemoteConnectionPanel(EditorWindow owner, Action repaint)
        {
            _expanded = EditorPrefs.GetBool(ExpandedPreferenceKey, true);
            _selectedMethodId = EditorPrefs.GetString(SelectedMethodPreferenceKey, string.Empty);
            _contributions = RemoteConnectionPanelContributionRegistry.CreateContributions(owner, repaint);
        }

        public void Draw(RemoteDevUtilitiesClient client)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            bool expanded = EditorGUILayout.Foldout(_expanded, BuildHeader(client), true, EditorStyles.foldoutHeader);
            if (GUILayout.Button(
                    new GUIContent("Settings", "Open the Remote Dev Utilities project settings."),
                    EditorStyles.miniButton,
                    GUILayout.Width(68f)))
            {
                RemoteDevUtilitiesProjectSettings.Open();
            }
            EditorGUILayout.EndHorizontal();
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

            if (client.IsConnected || client.IsHandshakePending)
            {
                DrawActiveConnection(client);
            }
            else if (_contributions.Count == 0)
            {
                EditorGUILayout.HelpBox("No remote connection transports are installed.", MessageType.Info);
            }
            else
            {
                DrawConnectionMethods(client);
            }

            if (!string.IsNullOrEmpty(client.ConnectionError))
                EditorGUILayout.HelpBox(client.ConnectionError, MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        public void Dispose()
        {
            for (int i = _contributions.Count - 1; i >= 0; i--)
                _contributions[i].Contribution.Dispose();
        }

        private static string BuildHeader(RemoteDevUtilitiesClient client)
        {
            if (client.IsConnected)
            {
                string target = string.IsNullOrWhiteSpace(client.SelectedTargetName) ? "Connected" : client.SelectedTargetName;
                return $"Connection - {target}";
            }
            return client.IsHandshakePending ? "Connection - Connecting" : "Connection";
        }

        private static void DrawActiveConnection(RemoteDevUtilitiesClient client)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            Color previousColor = GUI.contentColor;
            GUI.contentColor = client.IsConnected
                ? new Color(0.35f, 0.8f, 0.4f)
                : new Color(1f, 0.72f, 0.25f);
            GUILayout.Label(
                client.IsConnected ? "CONNECTED" : "CONNECTING",
                EditorStyles.miniBoldLabel,
                GUILayout.Width(82f));
            GUI.contentColor = previousColor;
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(client.SelectedTargetName)
                    ? "Remote Player"
                    : client.SelectedTargetName,
                EditorStyles.boldLabel);
            if (GUILayout.Button(client.IsConnected ? "Disconnect" : "Cancel", GUILayout.Width(90f)))
            {
                if (client.IsConnected)
                    client.Disconnect();
                else
                    client.CancelConnect();
            }
            EditorGUILayout.EndHorizontal();

            if (client.IsConnected && client.Target != null)
            {
                EditorGUILayout.Space(3f);
                DrawTarget(client.Target, client.SelectedTransportId);
            }
            else if (client.IsHandshakePending)
                EditorGUILayout.HelpBox("Waiting for the runtime agent handshake...", MessageType.None);
            EditorGUILayout.EndVertical();
        }

        private void DrawConnectionMethods(RemoteDevUtilitiesClient client)
        {
            EditorGUILayout.LabelField(
                "Choose how this Editor should connect to a Remote Dev Utilities Player.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            int selectedIndex = FindSelectedMethodIndex();
            var labels = new string[_contributions.Count];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = _contributions[i].Registration.DisplayName;

            int nextIndex = GUILayout.Toolbar(selectedIndex, labels);
            if (nextIndex != selectedIndex)
            {
                selectedIndex = nextIndex;
                _selectedMethodId = _contributions[selectedIndex].Registration.Id;
                EditorPrefs.SetString(SelectedMethodPreferenceKey, _selectedMethodId);
            }

            EditorGUILayout.Space(5f);
            _contributions[selectedIndex].Contribution.Draw(client);
        }

        private int FindSelectedMethodIndex()
        {
            for (int i = 0; i < _contributions.Count; i++)
            {
                if (string.Equals(
                        _contributions[i].Registration.Id,
                        _selectedMethodId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            _selectedMethodId = _contributions[0].Registration.Id;
            return 0;
        }

        private static void DrawTarget(RemoteTargetDescriptor target, string transportId)
        {
            EditorGUILayout.LabelField("Application", Join(target.ProductName, target.ApplicationVersion));
            EditorGUILayout.LabelField("Device", Join(target.Platform, target.DeviceName));
            EditorGUILayout.LabelField("Unity", target.UnityVersion ?? string.Empty);
            EditorGUILayout.LabelField("Transport", FormatTransport(transportId));
            if (!target.IsDevUtilitiesEnabled && !target.IsDebugBuild)
                EditorGUILayout.HelpBox("The target does not report complete Dev Utilities support.", MessageType.Warning);
            else if (!target.IsDebugBuild)
                EditorGUILayout.HelpBox("Connected to a non-Development Player with ENABLE_DEBUG.", MessageType.Info);
        }

        private static string Join(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return right ?? string.Empty;
            return string.IsNullOrWhiteSpace(right) ? left : left + " " + right;
        }

        private static string FormatTransport(string transportId)
        {
            return transportId switch
            {
                RemoteEditorTransportIds.PlayerConnection => "Unity Player Connection",
                RemoteEditorTransportIds.Tcp => "Direct TCP",
                _ => string.IsNullOrWhiteSpace(transportId) ? "Unknown" : transportId
            };
        }
    }
}

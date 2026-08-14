using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    internal sealed class RemoteConnectionPanel : IDisposable
    {
        private const string ExpandedPreferenceKey = "RemoteDevUtilities.ConnectionPanelExpanded";
        private readonly IReadOnlyList<IRemoteConnectionPanelContribution> _contributions;
        private bool _expanded;

        public RemoteConnectionPanel(EditorWindow owner, Action repaint)
        {
            _expanded = EditorPrefs.GetBool(ExpandedPreferenceKey, true);
            _contributions = RemoteConnectionPanelContributionRegistry.CreateContributions(owner, repaint);
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
                for (int i = 0; i < _contributions.Count; i++)
                {
                    if (i > 0)
                        EditorGUILayout.Space(4f);
                    _contributions[i].Draw(client);
                }
            }

            if (!string.IsNullOrEmpty(client.ConnectionError))
                EditorGUILayout.HelpBox(client.ConnectionError, MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        public void Dispose()
        {
            for (int i = _contributions.Count - 1; i >= 0; i--)
                _contributions[i].Dispose();
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

        private static void DrawActiveConnection(RemoteDevUtilitiesClient client)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(client.IsConnected
                ? $"Connected: {client.SelectedTargetName}"
                : $"Connecting: {client.SelectedTargetName}", EditorStyles.miniLabel);
            if (GUILayout.Button(client.IsConnected ? "Disconnect" : "Cancel", GUILayout.Width(90f)))
            {
                if (client.IsConnected)
                    client.Disconnect();
                else
                    client.CancelConnect();
            }
            EditorGUILayout.EndHorizontal();

            if (client.IsConnected && client.Target != null)
                DrawTarget(client.Target);
            else if (client.IsHandshakePending)
                EditorGUILayout.LabelField("Waiting for the runtime agent handshake…", EditorStyles.miniLabel);
        }

        private static void DrawTarget(RemoteTargetDescriptor target)
        {
            string status = $"{target.ProductName} {target.ApplicationVersion}  |  " +
                            $"{target.Platform}  |  Unity {target.UnityVersion}  |  {target.DeviceName}";
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
            if (!target.IsDevUtilitiesEnabled && !target.IsDebugBuild)
                EditorGUILayout.HelpBox("The target does not report complete Dev Utilities support.", MessageType.Warning);
            else if (!target.IsDebugBuild)
                EditorGUILayout.HelpBox("Connected to a non-Development Player with ENABLE_DEBUG.", MessageType.Info);
        }
    }
}

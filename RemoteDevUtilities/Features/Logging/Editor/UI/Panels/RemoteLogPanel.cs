using System;
using System.Collections.Generic;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.Commands;
using HP.Utilities.RemoteDevUtilities.Editor.Logging;
using HP.Utilities.RemoteDevUtilities.Editor.Logging.Settings;
using HP.Utilities.RemoteDevUtilities.Protocol.Logging;
using UnityEditor;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    [RemoteWorkspacePanel("logging", "Logs", 200)]
    internal sealed class RemoteLogPanel : IRemoteWorkspacePanel
    {
        private readonly RemoteLoggingTargetSettingsView _targetSettings = new RemoteLoggingTargetSettingsView();

        private string _filter = string.Empty;
        private bool _showLogs = true;
        private bool _showWarnings = true;
        private bool _showErrors = true;
        private bool _showStackTrace;
        private bool _autoScroll = true;

        public void Initialize(Action repaint)
        {
        }

        bool IRemoteWorkspacePanel.Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect)
        {
            RemoteLogClient logs = client.GetRequiredFeature<RemoteLogClient>();
            client.TryGetFeature(out IRemoteCommandExecutor commands);
            return Draw(logs, commands, connected);
        }

        public void Deactivate()
        {
        }

        public void Dispose()
        {
        }

        internal bool Draw(RemoteLogClient client, IRemoteCommandExecutor commandClient, bool connected)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _filter = GUILayout.TextField(_filter, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(130f));
            GUILayout.Label("View:", EditorStyles.miniLabel);
            _showLogs = GUILayout.Toggle(_showLogs, "Log", EditorStyles.toolbarButton);
            _showWarnings = GUILayout.Toggle(_showWarnings, "Warning", EditorStyles.toolbarButton);
            _showErrors = GUILayout.Toggle(_showErrors, "Error", EditorStyles.toolbarButton);
            _showStackTrace = GUILayout.Toggle(_showStackTrace, "Show Stacks", EditorStyles.toolbarButton);
            _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear View", EditorStyles.toolbarButton))
                client.Clear();
            EditorGUILayout.EndHorizontal();

            _targetSettings.Draw(client, commandClient, connected);

            if (!connected)
            {
                EditorGUILayout.HelpBox("Connect to a runtime Player to stream logs.", MessageType.Info);
                return false;
            }

            IReadOnlyList<RemoteLogEntry> entries = client.Entries;
            int first = Mathf.Max(0, entries.Count - 600);
            for (int i = first; i < entries.Count; i++)
            {
                RemoteLogEntry entry = entries[i];
                if (!ShouldShow(entry))
                    continue;

                MessageType type = ToMessageType(entry.LogType);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{entry.Sequence}  frame {entry.Frame}  {((LogType)entry.LogType)}", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(entry.Message ?? string.Empty, EditorStyles.wordWrappedLabel);
                if (_showStackTrace && !string.IsNullOrWhiteSpace(entry.StackTrace))
                    EditorGUILayout.HelpBox(entry.StackTrace, type);
                EditorGUILayout.EndVertical();
            }

            return _autoScroll && Event.current.type == EventType.Repaint;
        }

        private bool ShouldShow(RemoteLogEntry entry)
        {
            LogType type = (LogType)entry.LogType;
            bool allowed = type switch
            {
                LogType.Warning => _showWarnings,
                LogType.Error or LogType.Assert or LogType.Exception => _showErrors,
                _ => _showLogs
            };

            if (!allowed || string.IsNullOrWhiteSpace(_filter))
                return allowed;

            return (entry.Message?.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (entry.StackTrace?.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        private static MessageType ToMessageType(int logType)
        {
            return (LogType)logType switch
            {
                LogType.Warning => MessageType.Warning,
                LogType.Error or LogType.Assert or LogType.Exception => MessageType.Error,
                _ => MessageType.Info
            };
        }
    }
}

using SAS.Utilities.RemoteDevUtilities.Editor.Logging;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Logging.Settings
{
    internal sealed class RemoteLoggingTargetSettingsView
    {
        private readonly RemoteLoggingTagFilterEditor _tagFilterEditor = new RemoteLoggingTagFilterEditor();

        private bool _expanded;
        private bool _awaitingResult;
        private long _pendingRequestId;
        private string _resultMessage;
        private MessageType _resultType;
        private RemoteStackTraceTarget _stackTraceTarget = RemoteStackTraceTarget.All;
        private StackTraceLogType _stackTraceMode = StackTraceLogType.ScriptOnly;

        internal void Draw(RemoteLogClient logClient, IRemoteCommandExecutor commandClient, bool connected)
        {
            CaptureResult(logClient, commandClient);
            if (!connected)
            {
                _awaitingResult = false;
                _pendingRequestId = 0;
                _resultMessage = null;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _expanded = EditorGUILayout.Foldout(_expanded, "Target Logging Settings", true, EditorStyles.foldoutHeader);

            if (!_expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            bool commandAvailable = connected && RemoteLoggingCommandBuilder.IsAvailable(commandClient);

            if (!connected)
            {
                EditorGUILayout.HelpBox("Connect to a runtime Player to apply target logging settings.", MessageType.Info);
            }
            else if (!commandAvailable)
            {
                string message = commandClient == null
                    ? "Install Remote Commands to edit target logging settings. Log streaming remains available."
                    : string.IsNullOrWhiteSpace(commandClient.Error)
                        ? "The Logging command is not available in the current target command catalog."
                        : commandClient.Error;
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (commandClient != null && GUILayout.Button("Refresh Command Catalog", GUILayout.Width(165f)))
                    commandClient.RequestCatalog();
            }

            EditorGUILayout.BeginHorizontal();
            DrawLogLevelsColumn(logClient, commandClient, commandAvailable, connected);
            GUILayout.Space(6f);
            DrawStackTraceColumn(commandClient, commandAvailable);
            GUILayout.Space(6f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(240f), GUILayout.ExpandWidth(true));
            _tagFilterEditor.Draw(commandAvailable, command => Execute(commandClient, command));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_resultMessage))
                EditorGUILayout.HelpBox(_resultMessage, _resultType);

            EditorGUILayout.EndVertical();
        }

        private void DrawLogLevelsColumn(RemoteLogClient logClient, IRemoteCommandExecutor commandClient, bool commandAvailable, bool connected)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(190f));
            EditorGUILayout.LabelField("Debug Log Levels", EditorStyles.boldLabel);
            if (connected && !logClient.HasTargetSettings)
            {
                EditorGUILayout.HelpBox("Waiting for the Player's current log-level state.", MessageType.Info);
                if (GUILayout.Button("Refresh Status"))
                    logClient.RequestSettings();
            }

            DrawLogLevel(logClient, commandClient, commandAvailable, RemoteLoggingLevel.Info);
            DrawLogLevel(logClient, commandClient, commandAvailable, RemoteLoggingLevel.Warning);
            DrawLogLevel(logClient, commandClient, commandAvailable, RemoteLoggingLevel.Error);
            EditorGUILayout.EndVertical();
        }

        private void DrawStackTraceColumn(IRemoteCommandExecutor commandClient, bool commandAvailable)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(230f));
            EditorGUILayout.LabelField("Stack Traces", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Log Type", GUILayout.Width(58f));
            _stackTraceTarget = (RemoteStackTraceTarget)EditorGUILayout.EnumPopup(_stackTraceTarget);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Mode", GUILayout.Width(58f));
            _stackTraceMode = (StackTraceLogType)EditorGUILayout.EnumPopup(_stackTraceMode);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(!commandAvailable))
            {
                if (GUILayout.Button("Apply Stack Trace"))
                    Execute(commandClient, RemoteLoggingCommandBuilder.SetStackTrace(_stackTraceTarget, _stackTraceMode));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawLogLevel(RemoteLogClient logClient, IRemoteCommandExecutor commandClient, bool canExecute, RemoteLoggingLevel level)
        {
            bool enabled = IsEnabled(logClient, level);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(level.ToString(), GUILayout.Width(80f));
            using (new EditorGUI.DisabledScope(!canExecute || !logClient.HasTargetSettings || _awaitingResult))
            {
                bool requested = GUILayout.Toggle(enabled, enabled ? "Enabled" : "Disabled", GUI.skin.button, GUILayout.Width(90f));
                if (requested != enabled)
                {
                    Execute(commandClient, RemoteLoggingCommandBuilder.SetLogLevel(level, requested));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void Execute(IRemoteCommandExecutor commandClient, string command)
        {
            _resultMessage = null;
            _pendingRequestId = commandClient.Execute(command);
            _awaitingResult = _pendingRequestId != 0;
        }

        private void CaptureResult(RemoteLogClient logClient, IRemoteCommandExecutor commandClient)
        {
            if (!_awaitingResult || commandClient?.ExecutionResult == null || commandClient.ExecutionResultRequestId != _pendingRequestId)
                return;

            RemoteCommandExecutionResult result = commandClient.ExecutionResult;
            _awaitingResult = false;
            _pendingRequestId = 0;
            _resultMessage = result.Message ?? "Command completed.";
            _resultType = result.Success ? MessageType.Info : MessageType.Error;
            logClient.RequestSettings();
        }

        private static bool IsEnabled(RemoteLogClient logClient, RemoteLoggingLevel level)
        {
            return level switch
            {
                RemoteLoggingLevel.Info => logClient.InfoEnabled,
                RemoteLoggingLevel.Warning => logClient.WarningEnabled,
                RemoteLoggingLevel.Error => logClient.ErrorEnabled,
                _ => false
            };
        }
    }
}

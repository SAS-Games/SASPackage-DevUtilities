using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Logging.Settings
{
    internal sealed class RemoteLoggingTargetSettingsView
    {
        private readonly RemoteLoggingTagFilterEditor _tagFilterEditor =
            new RemoteLoggingTagFilterEditor();

        private bool _expanded;
        private bool _awaitingResult;
        private long _pendingRequestId;
        private string _resultMessage;
        private MessageType _resultType;
        private RemoteStackTraceTarget _stackTraceTarget = RemoteStackTraceTarget.All;
        private StackTraceLogType _stackTraceMode = StackTraceLogType.ScriptOnly;

        internal void Draw(RemoteCommandClient commandClient, bool connected)
        {
            CaptureResult(commandClient);
            if (!connected)
            {
                _awaitingResult = false;
                _pendingRequestId = 0;
                _resultMessage = null;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _expanded = EditorGUILayout.Foldout(
                _expanded,
                "Target Logging Settings",
                true,
                EditorStyles.foldoutHeader);

            if (!_expanded)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            bool commandAvailable =
                connected && RemoteLoggingCommandBuilder.IsAvailable(commandClient.Commands);

            if (!connected)
            {
                EditorGUILayout.HelpBox(
                    "Connect to a runtime Player to apply target logging settings.",
                    MessageType.Info);
            }
            else if (!commandAvailable)
            {
                string message = string.IsNullOrWhiteSpace(commandClient.Error)
                    ? "The Logging command is not available in the current target command catalog."
                    : commandClient.Error;
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (GUILayout.Button("Refresh Command Catalog", GUILayout.Width(165f)))
                    commandClient.RequestCatalog();
            }

            EditorGUILayout.LabelField(
                "These controls send actions to the Player; the current target state is not queried.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.LabelField("SAS.Debug Log Levels", EditorStyles.boldLabel);
            DrawLogLevel(commandClient, commandAvailable, RemoteLoggingLevel.Info);
            DrawLogLevel(commandClient, commandAvailable, RemoteLoggingLevel.Warning);
            DrawLogLevel(commandClient, commandAvailable, RemoteLoggingLevel.Error);

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Stack Traces", EditorStyles.boldLabel);
            _stackTraceTarget = (RemoteStackTraceTarget)EditorGUILayout.EnumPopup(
                "Log Type",
                _stackTraceTarget);
            _stackTraceMode = (StackTraceLogType)EditorGUILayout.EnumPopup(
                "Mode",
                _stackTraceMode);
            using (new EditorGUI.DisabledScope(!commandAvailable))
            {
                if (GUILayout.Button("Apply Stack Trace", GUILayout.Width(135f)))
                {
                    Execute(
                        commandClient,
                        RemoteLoggingCommandBuilder.SetStackTrace(
                            _stackTraceTarget,
                            _stackTraceMode));
                }
            }

            _tagFilterEditor.Draw(
                commandAvailable,
                command => Execute(commandClient, command));

            if (!string.IsNullOrWhiteSpace(_resultMessage))
                EditorGUILayout.HelpBox(_resultMessage, _resultType);

            EditorGUILayout.EndVertical();
        }

        private void DrawLogLevel(
            RemoteCommandClient commandClient,
            bool canExecute,
            RemoteLoggingLevel level)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(level.ToString(), GUILayout.Width(80f));
            using (new EditorGUI.DisabledScope(!canExecute))
            {
                if (GUILayout.Button("Enable", GUILayout.Width(70f)))
                {
                    Execute(
                        commandClient,
                        RemoteLoggingCommandBuilder.SetLogLevel(level, true));
                }

                if (GUILayout.Button("Disable", GUILayout.Width(70f)))
                {
                    Execute(
                        commandClient,
                        RemoteLoggingCommandBuilder.SetLogLevel(level, false));
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void Execute(RemoteCommandClient commandClient, string command)
        {
            _resultMessage = null;
            _pendingRequestId = commandClient.Execute(command);
            _awaitingResult = _pendingRequestId != 0;
        }

        private void CaptureResult(RemoteCommandClient commandClient)
        {
            if (!_awaitingResult ||
                commandClient.LastResult == null ||
                commandClient.LastResultRequestId != _pendingRequestId)
                return;

            _awaitingResult = false;
            _pendingRequestId = 0;
            _resultMessage = commandClient.LastResult.Message ?? "Command completed.";
            _resultType =
                commandClient.LastResult.Success ? MessageType.Info : MessageType.Error;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Sequences;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands.UI.Panels
{
    internal sealed class RemoteCommandSequencePanel : IDisposable
    {
        private const float StatusWidth = 145f;
        private Action _repaint;
        private RemoteDevUtilitiesClient _runnerClient;
        private RemoteCommandSequenceRunner _runner;
        private RemoteCommandSequence _sequence;
        private RemoteCommandSequence _runSequence;
        private SerializedObject _serializedSequence;
        private string _message;
        private MessageType _messageType = MessageType.Info;

        public void Initialize(Action repaint) => _repaint = repaint;

        internal void Draw(RemoteDevUtilitiesClient client, bool connected)
        {
            EnsureRunner(client);
            if (!connected && _runner?.IsRunning == true)
                _runner.Abort("The Player disconnected while the sequence was running.");

            DrawToolbar();
            if (_sequence == null)
            {
                EditorGUILayout.HelpBox(
                    "Create or select a Remote Command Sequence asset. Sequence assets stay in the Editor and can run against any connected Player with matching commands.",
                    MessageType.Info);
                return;
            }

            EnsureSerializedSequence();
            RemoteCommandClient commands = client.GetRequiredFeature<RemoteCommandClient>();
            DrawSequenceEditor(commands, connected);
            DrawExecutionControls(commands, connected);
            DrawRunSummary();
        }

        public void Dispose()
        {
            DisposeRunner();
            _serializedSequence = null;
            _sequence = null;
            _runSequence = null;
            _repaint = null;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            RemoteCommandSequence selected = (RemoteCommandSequence)EditorGUILayout.ObjectField(
                _sequence, typeof(RemoteCommandSequence), false, GUILayout.MinWidth(180f));
            if (EditorGUI.EndChangeCheck())
                SelectSequence(selected);

            if (GUILayout.Button("Find", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                ShowSequenceMenu();
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                CreateSequence();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSequenceEditor(RemoteCommandClient commands, bool connected)
        {
            _serializedSequence.Update();
            SerializedProperty description = _serializedSequence.FindProperty("m_Description");
            SerializedProperty stopOnFailure = _serializedSequence.FindProperty("m_StopOnFailure");
            SerializedProperty steps = _serializedSequence.FindProperty("m_Steps");

            using (new EditorGUI.DisabledScope(_runner?.IsRunning == true))
            {
                EditorGUILayout.PropertyField(description);
                EditorGUILayout.PropertyField(stopOnFailure, new GUIContent(
                    "Stop On Failure", "Skip remaining commands after the first failed command."));
                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField($"Steps ({steps.arraySize})", EditorStyles.boldLabel);

                for (int i = 0; i < steps.arraySize; i++)
                    DrawStep(steps, i, commands, connected);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add From Player...", GUILayout.Width(130f)))
                    ShowCommandMenu(commands, connected, steps.arraySize);
                if (GUILayout.Button("Add Empty", GUILayout.Width(90f)))
                    AddStep(string.Empty, steps.arraySize);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (_serializedSequence.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_sequence);
                if (_runner?.IsRunning != true)
                    _runSequence = null;
            }
        }

        private void DrawStep(
            SerializedProperty steps,
            int index,
            RemoteCommandClient commands,
            bool connected)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(index);
            SerializedProperty enabled = step.FindPropertyRelative("m_Enabled");
            SerializedProperty commandLine = step.FindPropertyRelative("m_CommandLine");
            SerializedProperty whenUnavailable = step.FindPropertyRelative("m_WhenUnavailable");
            SerializedProperty waitTimeout = step.FindPropertyRelative("m_WaitTimeoutSeconds");
            var runtimeStep = new RemoteCommandSequenceStep(
                commandLine.stringValue,
                enabled.boolValue,
                (RemoteCommandUnavailablePolicy)whenUnavailable.enumValueIndex,
                waitTimeout.floatValue);
            RemoteCommandSequenceStepValidation validation = RemoteCommandSequenceValidator.Validate(
                runtimeStep, connected, commands.Prefix, commands.Commands);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            enabled.boolValue = EditorGUILayout.Toggle(enabled.boolValue, GUILayout.Width(18f));
            commandLine.stringValue = EditorGUILayout.TextField(commandLine.stringValue ?? string.Empty);
            DrawValidation(validation);

            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                {
                    steps.MoveArrayElement(index, index - 1);
                    _serializedSequence.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_sequence);
                    _runSequence = null;
                    GUIUtility.ExitGUI();
                }
            }
            using (new EditorGUI.DisabledScope(index >= steps.arraySize - 1))
            {
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(24f)))
                {
                    steps.MoveArrayElement(index, index + 1);
                    _serializedSequence.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_sequence);
                    _runSequence = null;
                    GUIUtility.ExitGUI();
                }
            }
            if (GUILayout.Button("×", EditorStyles.miniButtonRight, GUILayout.Width(24f)))
            {
                steps.DeleteArrayElementAtIndex(index);
                _serializedSequence.ApplyModifiedProperties();
                EditorUtility.SetDirty(_sequence);
                _runSequence = null;
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18f);
            EditorGUILayout.PropertyField(
                whenUnavailable,
                new GUIContent("If Unavailable", "Fail immediately or wait for a scene-specific command to register."));
            if ((RemoteCommandUnavailablePolicy)whenUnavailable.enumValueIndex ==
                RemoteCommandUnavailablePolicy.WaitUntilAvailable)
            {
                EditorGUILayout.PropertyField(
                    waitTimeout,
                    new GUIContent("Timeout (s)"),
                    GUILayout.Width(150f));
            }
            EditorGUILayout.EndHorizontal();

            RemoteCommandSequenceStepResult runResult = FindRunResult(index);
            if (runResult != null)
                DrawStepResult(runResult);
            EditorGUILayout.EndVertical();
        }

        private static void DrawValidation(RemoteCommandSequenceStepValidation validation)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = validation.Availability switch
            {
                RemoteCommandSequenceStepAvailability.Ready => new Color(0.35f, 0.75f, 0.35f),
                RemoteCommandSequenceStepAvailability.Empty => new Color(1f, 0.65f, 0.25f),
                RemoteCommandSequenceStepAvailability.MissingCommand => new Color(1f, 0.4f, 0.35f),
                _ => previous
            };
            GUILayout.Label(validation.Message, EditorStyles.miniLabel, GUILayout.Width(StatusWidth));
            GUI.contentColor = previous;
        }

        private static void DrawStepResult(RemoteCommandSequenceStepResult result)
        {
            string state = result.State switch
            {
                RemoteCommandSequenceStepState.WaitingForCommand => "Waiting",
                RemoteCommandSequenceStepState.Running => "Running",
                RemoteCommandSequenceStepState.Succeeded => "Succeeded",
                RemoteCommandSequenceStepState.Failed => "Failed",
                RemoteCommandSequenceStepState.Skipped => "Skipped",
                _ => "Pending"
            };
            string message = string.IsNullOrWhiteSpace(result.Message)
                ? state
                : state + ": " + result.Message;
            MessageType type = result.State == RemoteCommandSequenceStepState.Failed
                ? MessageType.Error
                : result.State == RemoteCommandSequenceStepState.Succeeded
                    ? MessageType.Info
                    : MessageType.None;
            EditorGUILayout.HelpBox(message, type);
        }

        private void DrawExecutionControls(RemoteCommandClient commands, bool connected)
        {
            EditorGUILayout.Space(7f);
            if (!string.IsNullOrWhiteSpace(_message))
                EditorGUILayout.HelpBox(_message, _messageType);

            EditorGUILayout.BeginHorizontal();
            bool running = _runner?.IsRunning == true;
            using (new EditorGUI.DisabledScope(!connected || running))
            {
                if (GUILayout.Button("Validate", GUILayout.Width(85f)))
                    ValidateForRun(commands, connected, true);
                if (GUILayout.Button("Run Sequence", GUILayout.Width(110f)))
                    Run(commands, connected);
            }

            using (new EditorGUI.DisabledScope(!running || _runner.IsCancellationRequested))
            {
                if (GUILayout.Button(_runner?.IsCancellationRequested == true ? "Cancelling..." : "Cancel", GUILayout.Width(90f)))
                    _runner.Cancel();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRunSummary()
        {
            if (_runner == null || _runSequence != _sequence || _runner.State == RemoteCommandSequenceRunState.Idle)
                return;

            int succeeded = 0;
            int failed = 0;
            int skipped = 0;
            foreach (RemoteCommandSequenceStepResult result in _runner.Results)
            {
                switch (result.State)
                {
                    case RemoteCommandSequenceStepState.Succeeded:
                        succeeded++;
                        break;
                    case RemoteCommandSequenceStepState.Failed:
                        failed++;
                        break;
                    case RemoteCommandSequenceStepState.Skipped:
                        skipped++;
                        break;
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"Sequence: {_runner.State}  |  Succeeded: {succeeded}  Failed: {failed}  Skipped: {skipped}",
                EditorStyles.miniBoldLabel);
        }

        private void Run(RemoteCommandClient commands, bool connected)
        {
            if (!ValidateForRun(commands, connected, false))
                return;

            _runSequence = _sequence;
            if (!_runner.Start(_sequence, out string error))
            {
                SetMessage(error, MessageType.Error);
                return;
            }
            if (_runner.IsRunning)
                SetMessage($"Running '{_sequence.name}'.", MessageType.Info);
        }

        private bool ValidateForRun(RemoteCommandClient commands, bool connected, bool showSuccess)
        {
            if (!connected)
            {
                SetMessage("Connect to a runtime Player before validating or running a sequence.", MessageType.Warning);
                return false;
            }
            if (_sequence == null)
            {
                SetMessage("Select a command sequence.", MessageType.Warning);
                return false;
            }

            int enabledCount = 0;
            int unavailableCount = 0;
            IReadOnlyList<RemoteCommandSequenceStep> steps = _sequence.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                RemoteCommandSequenceStepValidation validation = RemoteCommandSequenceValidator.Validate(
                    steps[i], connected, commands.Prefix, commands.Commands);
                if (steps[i]?.Enabled == true)
                    enabledCount++;
                if (steps[i]?.Enabled == true &&
                    validation.Availability == RemoteCommandSequenceStepAvailability.MissingCommand)
                {
                    unavailableCount++;
                }
                if (!validation.BlocksExecution)
                    continue;

                SetMessage($"Step {i + 1}: {validation.Message}.", MessageType.Error);
                return false;
            }

            if (enabledCount == 0)
            {
                SetMessage("The sequence does not contain any enabled commands.", MessageType.Warning);
                return false;
            }

            if (showSuccess)
            {
                if (unavailableCount > 0)
                {
                    SetMessage(
                        $"'{_sequence.name}' is structurally valid. {unavailableCount} command(s) are currently unavailable and will be handled when reached.",
                        MessageType.Warning);
                }
                else
                {
                    SetMessage($"'{_sequence.name}' is valid for the connected Player.", MessageType.Info);
                }
            }
            return true;
        }

        private void ShowCommandMenu(RemoteCommandClient commands, bool connected, int insertIndex)
        {
            var menu = new GenericMenu();
            if (!connected || commands.Commands.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("Connect to a Player with runtime commands"));
                menu.ShowAsContext();
                return;
            }

            var descriptors = (RemoteCommandDescriptor[])commands.Commands.Clone();
            Array.Sort(descriptors, (left, right) => string.Compare(
                left?.Name, right?.Name, StringComparison.OrdinalIgnoreCase));
            foreach (RemoteCommandDescriptor descriptor in descriptors)
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Name))
                    continue;

                string commandName = descriptor.Name.Trim();
                string rootPath = EscapeMenuSegment(commandName);
                string rootLine = commandName;
                menu.AddItem(new GUIContent(rootPath + "/Command"), false,
                    () => AddStep(rootLine, insertIndex));

                string[] presets = descriptor.Presets ?? Array.Empty<string>();
                for (int i = 0; i < presets.Length; i++)
                {
                    string preset = presets[i]?.Trim();
                    if (string.IsNullOrWhiteSpace(preset))
                        continue;
                    string presetLine = preset;
                    menu.AddItem(new GUIContent(rootPath + "/Presets/" + EscapeMenuSegment(preset)), false,
                        () => AddStep(presetLine, insertIndex));
                }
            }
            menu.ShowAsContext();
        }

        private void AddStep(string commandLine, int insertIndex)
        {
            if (_sequence == null || _runner?.IsRunning == true)
                return;
            EnsureSerializedSequence();
            _serializedSequence.Update();
            SerializedProperty steps = _serializedSequence.FindProperty("m_Steps");
            int index = Mathf.Clamp(insertIndex, 0, steps.arraySize);
            steps.InsertArrayElementAtIndex(index);
            SerializedProperty step = steps.GetArrayElementAtIndex(index);
            step.FindPropertyRelative("m_Enabled").boolValue = true;
            step.FindPropertyRelative("m_CommandLine").stringValue = commandLine ?? string.Empty;
            step.FindPropertyRelative("m_WhenUnavailable").enumValueIndex =
                (int)RemoteCommandUnavailablePolicy.FailImmediately;
            step.FindPropertyRelative("m_WaitTimeoutSeconds").floatValue = 5f;
            _serializedSequence.ApplyModifiedProperties();
            EditorUtility.SetDirty(_sequence);
            _runSequence = null;
            _repaint?.Invoke();
        }

        private void EnsureRunner(RemoteDevUtilitiesClient client)
        {
            if (ReferenceEquals(_runnerClient, client) && _runner != null)
                return;

            DisposeRunner();
            _runnerClient = client;
            _runner = new RemoteCommandSequenceRunner(new RemoteCommandSequenceDispatcher(client));
            _runner.Changed += OnRunnerChanged;
        }

        private void DisposeRunner()
        {
            if (_runner != null)
            {
                _runner.Changed -= OnRunnerChanged;
                _runner.Dispose();
            }
            _runner = null;
            _runnerClient = null;
        }

        private void OnRunnerChanged()
        {
            if (_runner != null && !_runner.IsRunning && _runSequence != null)
            {
                MessageType type = _runner.State == RemoteCommandSequenceRunState.Completed
                    ? MessageType.Info
                    : _runner.State == RemoteCommandSequenceRunState.Cancelled
                        ? MessageType.Warning
                        : MessageType.Error;
                SetMessage($"'{_runSequence.name}' {_runner.State.ToString().ToLowerInvariant()}.", type);
            }
            _repaint?.Invoke();
        }

        private RemoteCommandSequenceStepResult FindRunResult(int sourceIndex)
        {
            if (_runner == null || _runSequence != _sequence)
                return null;
            foreach (RemoteCommandSequenceStepResult result in _runner.Results)
            {
                if (result.SourceIndex == sourceIndex)
                    return result;
            }
            return null;
        }

        private void SelectSequence(RemoteCommandSequence sequence)
        {
            _sequence = sequence;
            _serializedSequence = sequence == null ? null : new SerializedObject(sequence);
            _message = null;
        }

        private void EnsureSerializedSequence()
        {
            if (_serializedSequence == null || _serializedSequence.targetObject != _sequence)
                _serializedSequence = new SerializedObject(_sequence);
        }

        private void CreateSequence()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Remote Command Sequence",
                "Remote Command Sequence",
                "asset",
                "Choose where to save the shared Editor command sequence.");
            if (string.IsNullOrWhiteSpace(path))
                return;

            var sequence = ScriptableObject.CreateInstance<RemoteCommandSequence>();
            AssetDatabase.CreateAsset(sequence, path);
            AssetDatabase.SaveAssets();
            SelectSequence(sequence);
            Selection.activeObject = sequence;
            EditorGUIUtility.PingObject(sequence);
        }

        private void ShowSequenceMenu()
        {
            string[] guids = AssetDatabase.FindAssets("t:RemoteCommandSequence");
            var menu = new GenericMenu();
            if (guids.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No sequence assets found"));
            }
            else
            {
                Array.Sort(guids, (left, right) => string.Compare(
                    AssetDatabase.GUIDToAssetPath(left), AssetDatabase.GUIDToAssetPath(right),
                    StringComparison.OrdinalIgnoreCase));
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    RemoteCommandSequence sequence = AssetDatabase.LoadAssetAtPath<RemoteCommandSequence>(path);
                    if (sequence == null)
                        continue;
                    string label = Path.GetFileNameWithoutExtension(path) + "  —  " + path;
                    menu.AddItem(new GUIContent(EscapeMenuSegment(label)), sequence == _sequence,
                        () => SelectSequence(sequence));
                }
            }
            menu.ShowAsContext();
        }

        private void SetMessage(string message, MessageType type)
        {
            _message = message;
            _messageType = type;
            _repaint?.Invoke();
        }

        private static string EscapeMenuSegment(string value) =>
            (value ?? string.Empty).Replace("/", "∕");
    }
}

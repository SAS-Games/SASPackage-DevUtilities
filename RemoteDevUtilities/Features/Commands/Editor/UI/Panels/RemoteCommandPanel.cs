using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.UI.Panels;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.UI.Panels
{
    [RemoteWorkspacePanel("commands", "Commands", 100)]
    internal sealed class RemoteCommandPanel : IRemoteWorkspacePanel
    {
        private const string CommandControlName = "RemoteDevUtilities.Command";
        private static readonly string[] ViewLabels = { "Commands", "Sequences" };
        private string _command = string.Empty;
        private string _filter = string.Empty;
        private readonly HashSet<string> _expandedCommands = new(StringComparer.OrdinalIgnoreCase);
        private readonly RemoteCommandSequencePanel _sequences = new();
        private int _selectedView;
        private RemoteDevUtilitiesClient _coordinatorClient;
        private RemoteCommandPresentationCoordinator _coordinator;

        public void Initialize(Action repaint)
        {
            _sequences.Initialize(repaint);
        }

        bool IRemoteWorkspacePanel.Draw(RemoteDevUtilitiesClient client, bool connected, Rect windowRect)
        {
            _selectedView = GUILayout.Toolbar(_selectedView, ViewLabels, EditorStyles.toolbarButton);
            EditorGUILayout.Space(3f);
            if (_selectedView == 1)
            {
                _sequences.Draw(client, connected);
                return false;
            }

            if (!ReferenceEquals(_coordinatorClient, client))
            {
                _coordinatorClient = client;
                _coordinator = new RemoteCommandPresentationCoordinator(client);
            }

            Draw(client.GetRequiredFeature<RemoteCommandClient>(), _coordinator, connected);
            return false;
        }

        public void Deactivate()
        {
        }

        public void Dispose()
        {
            _sequences.Dispose();
            _coordinator = null;
            _coordinatorClient = null;
        }

        public void Draw(RemoteCommandClient client, RemoteCommandPresentationCoordinator coordinator, bool connected)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _filter = GUILayout.TextField(_filter, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(140f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand All", EditorStyles.toolbarButton))
                SetAllExpanded(client.Commands, true);
            if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton))
                SetAllExpanded(client.Commands, false);
            using (new EditorGUI.DisabledScope(!connected))
            {
                if (GUILayout.Button("Refresh Catalog", EditorStyles.toolbarButton))
                    client.RequestCatalog();
            }

            EditorGUILayout.EndHorizontal();

            if (!connected)
            {
                EditorGUILayout.HelpBox("Connect to a runtime Player to use commands.", MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(client.Error))
                EditorGUILayout.HelpBox(client.Error, MessageType.Warning);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (!string.IsNullOrEmpty(client.Prefix))
                GUILayout.Label(client.Prefix, GUILayout.Width(18f));
            GUI.SetNextControlName(CommandControlName);
            _command = EditorGUILayout.TextField(_command);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_command)))
            {
                if (GUILayout.Button("Execute", GUILayout.Width(80f)))
                    Execute(coordinator);
            }

            EditorGUILayout.EndHorizontal();

            Event current = Event.current;
            if (current.type == EventType.KeyDown && (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == CommandControlName && !string.IsNullOrWhiteSpace(_command))
            {
                Execute(coordinator);
                current.Use();
            }

            if (client.LastResult != null)
            {
                EditorGUILayout.HelpBox(client.LastResult.Message ?? string.Empty, client.LastResult.Success ? MessageType.Info : MessageType.Error);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Runtime Commands ({client.Commands.Length})", EditorStyles.boldLabel);

            foreach (RemoteCommandDescriptor command in client.Commands)
            {
                if (command == null || !Matches(command, _filter))
                    continue;

                DrawCommand(command, coordinator);
            }
        }

        private void DrawCommand(
            RemoteCommandDescriptor command,
            RemoteCommandPresentationCoordinator coordinator)
        {
            string commandName = command.Name ?? string.Empty;
            string[] presets = command.Presets ?? Array.Empty<string>();
            bool expanded = _expandedCommands.Contains(commandName);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            bool nextExpanded = EditorGUILayout.Foldout(expanded, string.IsNullOrWhiteSpace(commandName) ? "Unnamed Command" : commandName, true);
            SetExpanded(commandName, nextExpanded);

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{presets.Length} {(presets.Length == 1 ? "preset" : "presets")}", EditorStyles.miniLabel);
            GUILayout.Label(command.CloseOnCompletion ? "closes console" : "keeps console open", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(commandName)))
            {
                if (GUILayout.Button(
                        new GUIContent("Run", "Execute this command immediately without parameters."),
                        EditorStyles.miniButton,
                        GUILayout.Width(44f)))
                {
                    Execute(coordinator, commandName);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (nextExpanded)
            {
                EditorGUI.indentLevel++;
                if (!string.IsNullOrWhiteSpace(command.HelpText))
                {
                    EditorGUILayout.LabelField(command.HelpText, EditorStyles.wordWrappedMiniLabel);
                }

                DrawPresets(presets, coordinator);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPresets(
            string[] presets,
            RemoteCommandPresentationCoordinator coordinator)
        {
            if (presets.Length == 0)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);
            for (int i = 0; i < presets.Length; i++)
            {
                string preset = presets[i];
                if (string.IsNullOrWhiteSpace(preset))
                    continue;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(
                        new GUIContent(preset, "Copy this preset into the command input field."),
                        EditorStyles.miniButtonLeft,
                        GUILayout.ExpandWidth(true)))
                {
                    _command = preset;
                }
                if (GUILayout.Button(
                        new GUIContent("Run", "Execute this preset immediately."),
                        EditorStyles.miniButtonRight,
                        GUILayout.Width(44f)))
                {
                    Execute(coordinator, preset);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void Execute(RemoteCommandPresentationCoordinator coordinator)
        {
            Execute(coordinator, _command);
        }

        private static void Execute(
            RemoteCommandPresentationCoordinator coordinator,
            string commandLine)
        {
            coordinator.Execute((commandLine ?? string.Empty).Trim());
            GUI.FocusControl(CommandControlName);
        }

        private static bool Matches(RemoteCommandDescriptor command, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            if ((command.Name?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (command.HelpText?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                return true;

            string[] presets = command.Presets ?? Array.Empty<string>();
            for (int i = 0; i < presets.Length; i++)
            {
                if ((presets[i]?.IndexOf(filter, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    return true;
            }

            return false;
        }

        private void SetAllExpanded(IEnumerable<RemoteCommandDescriptor> commands, bool expanded)
        {
            _expandedCommands.Clear();
            if (!expanded || commands == null)
                return;

            foreach (RemoteCommandDescriptor command in commands)
            {
                if (command != null && !string.IsNullOrWhiteSpace(command.Name))
                    _expandedCommands.Add(command.Name);
            }
        }

        private void SetExpanded(string commandName, bool expanded)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                return;

            if (expanded)
                _expandedCommands.Add(commandName);
            else
                _expandedCommands.Remove(commandName);
        }
    }
}

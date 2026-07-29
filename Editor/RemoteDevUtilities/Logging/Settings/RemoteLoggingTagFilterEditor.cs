using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Logging.Settings
{
    internal sealed class RemoteLoggingTagFilterEditor
    {
        private readonly List<string> _tags = new List<string> { string.Empty };
        private string _validationError;

        internal void Draw(bool canExecute, Action<string> execute)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Tag Filters", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Target tags apply to SAS.Debug tagged logs. An empty list allows every tag.",
                EditorStyles.wordWrappedMiniLabel);

            int removeIndex = -1;
            for (int i = 0; i < _tags.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                string updated = EditorGUILayout.TextField($"Tag {i + 1}", _tags[i]);
                if (!string.Equals(updated, _tags[i], StringComparison.Ordinal))
                {
                    _tags[i] = updated;
                    _validationError = null;
                }

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }

            if (removeIndex >= 0)
            {
                _tags.RemoveAt(removeIndex);
                _validationError = null;
            }

            if (_tags.Count == 0)
                EditorGUILayout.LabelField("No tag filters.", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Filter", GUILayout.Width(90f)))
            {
                _tags.Add(string.Empty);
                _validationError = null;
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!canExecute))
            {
                if (GUILayout.Button("Apply Filters", GUILayout.Width(95f)))
                    Apply(execute);

                if (GUILayout.Button("Clear Target Tags", GUILayout.Width(120f)))
                {
                    _tags.Clear();
                    _validationError = null;
                    execute(RemoteLoggingCommandBuilder.ClearTagsCommand);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(_validationError))
                EditorGUILayout.HelpBox(_validationError, MessageType.Error);
        }

        private void Apply(Action<string> execute)
        {
            if (!RemoteLoggingCommandBuilder.TrySetTags(
                    _tags,
                    out string command,
                    out string[] normalizedTags,
                    out _validationError))
                return;

            _tags.Clear();
            _tags.AddRange(normalizedTags);
            execute(command);
        }
    }
}

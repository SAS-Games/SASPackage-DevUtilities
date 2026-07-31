using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteComponentInspectorView
    {
        private readonly HashSet<long> _expandedComponents = new();
        private readonly Dictionary<string, string> _editValues = new();

        public void Draw(
            RemoteRuntimeSceneInspectorClient client,
            RemoteComponentDescriptor[] components)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            foreach (RemoteComponentDescriptor component in components ??
                     Array.Empty<RemoteComponentDescriptor>())
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool expanded = _expandedComponents.Contains(component.Id);
                bool next = EditorGUILayout.Foldout(
                    expanded,
                    component.Missing
                        ? "Missing Script"
                        : RemoteInspectorFormatting.ShortTypeName(component.TypeName),
                    true);
                RemoteInspectorFormatting.SetExpanded(
                    _expandedComponents,
                    component.Id,
                    next);

                if (next)
                    DrawExpandedComponent(client, component);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawExpandedComponent(
            RemoteRuntimeSceneInspectorClient client,
            RemoteComponentDescriptor component)
        {
            if (component.HasEnabledState)
            {
                bool enabled = EditorGUILayout.Toggle("Enabled", component.Enabled);
                if (enabled != component.Enabled)
                {
                    client.Execute(new RemoteSceneInspectorCommandRequest
                    {
                        Kind = RemoteSceneInspectorCommandKind.SetComponentEnabled,
                        ComponentId = component.Id,
                        BooleanValue = enabled
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(component.StatusMessage))
                EditorGUILayout.HelpBox(component.StatusMessage, MessageType.Info);

            foreach (RemoteMemberDescriptor member in component.Members ??
                     Array.Empty<RemoteMemberDescriptor>())
                DrawMember(client, component.Id, member);
        }

        private void DrawMember(
            RemoteRuntimeSceneInspectorClient client,
            long componentId,
            RemoteMemberDescriptor member)
        {
            string key = $"component:{componentId}:{member.Name}";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(member.DisplayName ?? member.Name, GUILayout.Width(155f));
            if (member.ReadOnly)
            {
                EditorGUILayout.SelectableLabel(
                    member.Value ?? string.Empty,
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            else
            {
                if (!_editValues.TryGetValue(key, out string value))
                    value = member.Value ?? string.Empty;
                value = EditorGUILayout.TextField(value);
                _editValues[key] = value;
                using (new EditorGUI.DisabledScope(value == (member.Value ?? string.Empty)))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(48f)))
                    {
                        client.Execute(new RemoteSceneInspectorCommandRequest
                        {
                            Kind = RemoteSceneInspectorCommandKind.SetMemberValue,
                            ComponentId = componentId,
                            MemberName = member.Name,
                            Value = value
                        });
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(member.Error))
                EditorGUILayout.HelpBox(member.Error, MessageType.Warning);
        }
    }
}

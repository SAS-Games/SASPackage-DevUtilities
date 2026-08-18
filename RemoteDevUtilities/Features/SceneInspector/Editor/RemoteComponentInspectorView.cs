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

        public void Draw(RemoteRuntimeSceneInspectorClient client, RemoteComponentDescriptor[] components)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Components", EditorStyles.boldLabel);
            foreach (RemoteComponentDescriptor component in components ?? Array.Empty<RemoteComponentDescriptor>())
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool expanded = _expandedComponents.Contains(component.Id);
                bool next = EditorGUILayout.Foldout(expanded, component.Missing ? "Missing Script" : RemoteInspectorFormatting.ShortTypeName(component.TypeName), true);
                RemoteInspectorFormatting.SetExpanded(_expandedComponents, component.Id, next);

                if (next)
                    DrawExpandedComponent(client, component);

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawExpandedComponent(RemoteRuntimeSceneInspectorClient client, RemoteComponentDescriptor component)
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

            foreach (RemoteMemberDescriptor member in component.Members ?? Array.Empty<RemoteMemberDescriptor>())
                DrawMember(client, component.Id, member);
        }

        private void DrawMember(RemoteRuntimeSceneInspectorClient client, long componentId, RemoteMemberDescriptor member)
        {
            string key = $"component:{componentId}:{member.Name}";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(member.DisplayName ?? member.Name, GUILayout.Width(155f));
            if (member.ReadOnly)
            {
                EditorGUILayout.SelectableLabel(member.Value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            else if (TryGetEnumDropdown(member, out string[] enumOptions, out int enumSelectedIndex))
            {
                int nextIndex = EditorGUILayout.Popup(enumSelectedIndex, enumOptions);
                if (nextIndex != enumSelectedIndex)
                {
                    client.Execute(new RemoteSceneInspectorCommandRequest
                    {
                        Kind = RemoteSceneInspectorCommandKind.SetMemberValue,
                        ComponentId = componentId,
                        MemberName = member.Name,
                        Value = enumOptions[nextIndex]
                    });
                }
            }
            else if (TryGetSpecialDropdown(member, out string[] options, out int selectedIndex))
            {
                int nextIndex = EditorGUILayout.Popup(selectedIndex, options);
                if (nextIndex != selectedIndex)
                {
                    client.Execute(new RemoteSceneInspectorCommandRequest
                    {
                        Kind = RemoteSceneInspectorCommandKind.SetMemberValue,
                        ComponentId = componentId,
                        MemberName = member.Name,
                        Value = options[nextIndex]
                    });
                }
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

        private static bool TryGetEnumDropdown(RemoteMemberDescriptor member, out string[] options, out int selectedIndex)
        {
            options = null;
            selectedIndex = 0;
            if (member == null || string.IsNullOrWhiteSpace(member.TypeName))
                return false;

            Type enumType = ResolveEnumType(member.TypeName);
            if (enumType == null || !enumType.IsEnum)
                return false;

            options = Enum.GetNames(enumType);
            string current = member.Value ?? string.Empty;
            if (int.TryParse(current, out int numericValue))
            {
                object enumValue = Enum.ToObject(enumType, numericValue);
                current = enumValue.ToString();
            }

            selectedIndex = FindIndexCaseInsensitive(options, current);
            if (selectedIndex < 0)
                selectedIndex = 0;
            return true;
        }

        private static bool TryGetSpecialDropdown(RemoteMemberDescriptor member, out string[] options, out int selectedIndex)
        {
            options = null;
            selectedIndex = 0;
            if (member == null || string.IsNullOrWhiteSpace(member.Value))
                return false;

            string display = (member.DisplayName ?? member.Name ?? string.Empty).Replace(" ", string.Empty);
            if (display.IndexOf("SortingLayer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (display.IndexOf("Sorting", StringComparison.OrdinalIgnoreCase) >= 0 && display.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                SortingLayer[] sortingLayers = SortingLayer.layers;
                string[] layers = new string[sortingLayers.Length > 0 ? sortingLayers.Length : 1];
                for (int i = 0; i < sortingLayers.Length; i++)
                    layers[i] = sortingLayers[i].name;
                if (sortingLayers.Length == 0)
                    layers[0] = member.Value;

                options = layers;
                selectedIndex = System.Array.IndexOf(options, member.Value);
                if (selectedIndex < 0)
                    selectedIndex = 0;
                return true;
            }

            if (display.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0 &&
                display.IndexOf("Sorting", StringComparison.OrdinalIgnoreCase) < 0)
            {
                string[] layers = new string[32];
                for (int i = 0; i < 32; i++)
                {
                    string name = LayerMask.LayerToName(i);
                    layers[i] = string.IsNullOrEmpty(name) ? ("Layer " + i) : name;
                }
                options = layers;
                selectedIndex = System.Array.IndexOf(options, member.Value);
                if (selectedIndex < 0)
                    selectedIndex = 0;
                return true;
            }

            return false;
        }

        private static Type ResolveEnumType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            string[] candidates = new[]
            {
                typeName,
                typeName + ", UnityEngine.CoreModule",
                typeName + ", UnityEngine",
                typeName + ", UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                typeName + ", UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
            };

            foreach (string candidate in candidates)
            {
                Type type = Type.GetType(candidate, false);
                if (type != null)
                    return type;
            }

            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types = assembly.GetTypes();
                for (int i = 0; i < types.Length; i++)
                {
                    if (string.Equals(types[i].FullName, typeName, StringComparison.Ordinal) && types[i].IsEnum)
                        return types[i];
                }
            }

            return null;
        }

        private static int FindIndexCaseInsensitive(string[] options, string current)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(options[i], current, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }
    }
}

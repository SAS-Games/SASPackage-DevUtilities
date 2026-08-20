using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
                bool enabled;
                using (new EditorGUI.DisabledScope(component.EnabledReadOnly))
                    enabled = EditorGUILayout.Toggle("Enabled", component.Enabled);
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
            GUILayout.Label(GetMemberLabel(member), GUILayout.Width(155f));
            if (member.ReadOnly)
            {
                DrawReadOnlyMember(member);
            }
            else if (TryGetBooleanValue(member, out bool booleanValue))
            {
                bool nextValue = EditorGUILayout.Toggle(booleanValue);
                if (nextValue != booleanValue)
                {
                    SetMemberValue(client, componentId, member, nextValue.ToString());
                }
            }
            else if (TryGetEnumValue(member, out Enum enumValue, out bool isFlags))
            {
                Enum nextValue = isFlags
                    ? EditorGUILayout.EnumFlagsField(enumValue)
                    : EditorGUILayout.EnumPopup(enumValue);
                if (!Equals(nextValue, enumValue))
                {
                    SetMemberValue(client, componentId, member, nextValue.ToString());
                }
            }
            else if (TryGetLayerMaskValue(member, out int layerMask))
            {
                int nextMask = EditorGUILayout.MaskField(layerMask, BuildLayerNames());
                if (nextMask != layerMask)
                {
                    SetMemberValue(client, componentId, member,
                        nextMask.ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (TryGetSpecialDropdown(member, out string[] labels, out string[] values,
                         out int selectedIndex))
            {
                int nextIndex = EditorGUILayout.Popup(selectedIndex, labels);
                if (nextIndex != selectedIndex)
                {
                    SetMemberValue(client, componentId, member, values[nextIndex]);
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

        private static void DrawReadOnlyMember(RemoteMemberDescriptor member)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                if (TryGetBooleanValue(member, out bool booleanValue))
                    EditorGUILayout.Toggle(booleanValue);
                else if (TryGetEnumValue(member, out Enum enumValue, out bool isFlags))
                {
                    if (isFlags)
                        EditorGUILayout.EnumFlagsField(enumValue);
                    else
                        EditorGUILayout.EnumPopup(enumValue);
                }
                else if (TryGetLayerMaskValue(member, out int layerMask))
                    EditorGUILayout.MaskField(layerMask, BuildLayerNames());
                else if (TryGetSpecialDropdown(member, out string[] labels, out _, out int selectedIndex))
                    EditorGUILayout.Popup(selectedIndex, labels);
                else
                    EditorGUILayout.TextField(member?.Value ?? string.Empty);
            }
        }

        private static void SetMemberValue(RemoteRuntimeSceneInspectorClient client, long componentId,
            RemoteMemberDescriptor member, string value)
        {
            client.Execute(new RemoteSceneInspectorCommandRequest
            {
                Kind = RemoteSceneInspectorCommandKind.SetMemberValue,
                ComponentId = componentId,
                MemberName = member.Name,
                Value = value
            });
        }

        private static bool TryGetBooleanValue(RemoteMemberDescriptor member, out bool value)
        {
            value = false;
            return member != null && IsType(member.TypeName, typeof(bool)) &&
                   bool.TryParse(member.Value, out value);
        }

        private static bool TryGetLayerMaskValue(RemoteMemberDescriptor member, out int value)
        {
            value = 0;
            return member != null && IsType(member.TypeName, typeof(LayerMask)) &&
                   int.TryParse(member.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetEnumValue(RemoteMemberDescriptor member, out Enum value, out bool isFlags)
        {
            value = null;
            isFlags = false;
            if (member == null || string.IsNullOrWhiteSpace(member.TypeName))
                return false;

            Type enumType = ResolveEnumType(member.TypeName);
            if (enumType == null || !enumType.IsEnum)
                return false;

            try
            {
                string current = member.Value ?? string.Empty;
                object parsed = long.TryParse(current, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long numericValue)
                    ? Enum.ToObject(enumType, numericValue)
                    : Enum.Parse(enumType, current, true);
                value = parsed as Enum;
                isFlags = enumType.IsDefined(typeof(FlagsAttribute), false);
                return value != null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool TryGetSpecialDropdown(RemoteMemberDescriptor member, out string[] labels,
            out string[] values, out int selectedIndex)
        {
            labels = null;
            values = null;
            selectedIndex = 0;
            if (member == null || string.IsNullOrWhiteSpace(member.Value))
                return false;

            string display = (member.DisplayName ?? member.Name ?? string.Empty).Replace(" ", string.Empty);
            if (display.IndexOf("SortingLayer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (display.IndexOf("Sorting", StringComparison.OrdinalIgnoreCase) >= 0 && display.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                SortingLayer[] sortingLayers = SortingLayer.layers;
                labels = new string[sortingLayers.Length > 0 ? sortingLayers.Length : 1];
                values = new string[labels.Length];
                bool useNumericId = IsIntegralType(member.TypeName) ||
                                    display.EndsWith("ID", StringComparison.OrdinalIgnoreCase);
                for (int i = 0; i < sortingLayers.Length; i++)
                {
                    labels[i] = sortingLayers[i].name;
                    values[i] = useNumericId
                        ? sortingLayers[i].id.ToString(CultureInfo.InvariantCulture)
                        : sortingLayers[i].name;
                }
                if (sortingLayers.Length == 0)
                {
                    labels[0] = member.Value;
                    values[0] = member.Value;
                }

                selectedIndex = FindIndexCaseInsensitive(values, member.Value);
                if (selectedIndex < 0)
                    selectedIndex = FindIndexCaseInsensitive(labels, member.Value);
                if (selectedIndex < 0)
                    selectedIndex = 0;
                return true;
            }

            if (display.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0 &&
                display.IndexOf("Sorting", StringComparison.OrdinalIgnoreCase) < 0 &&
                display.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) < 0 &&
                IsIntegralType(member.TypeName))
            {
                labels = BuildLayerNames();
                values = new string[32];
                for (int i = 0; i < 32; i++)
                    values[i] = i.ToString(CultureInfo.InvariantCulture);
                selectedIndex = FindIndexCaseInsensitive(values, member.Value);
                if (selectedIndex < 0)
                    selectedIndex = FindIndexCaseInsensitive(labels, member.Value);
                if (selectedIndex < 0)
                    selectedIndex = 0;
                return true;
            }

            return false;
        }

        private static string[] BuildLayerNames()
        {
            var layers = new string[32];
            for (int i = 0; i < layers.Length; i++)
            {
                string name = LayerMask.LayerToName(i);
                layers[i] = string.IsNullOrEmpty(name) ? ("Layer " + i) : name;
            }
            return layers;
        }

        private static string GetMemberLabel(RemoteMemberDescriptor member)
        {
            string label = member?.DisplayName ?? member?.Name ?? string.Empty;
            string normalized = label.Replace(" ", string.Empty);
            return string.Equals(normalized, "SortingLayerID", StringComparison.OrdinalIgnoreCase)
                ? "Sorting Layer"
                : label;
        }

        private static bool IsIntegralType(string typeName) =>
            IsType(typeName, typeof(byte)) || IsType(typeName, typeof(short)) ||
            IsType(typeName, typeof(int)) || IsType(typeName, typeof(long));

        private static bool IsType(string typeName, Type type) =>
            string.Equals(typeName, type.FullName, StringComparison.Ordinal) ||
            string.Equals(typeName, type.Name, StringComparison.Ordinal) ||
            string.Equals(typeName, type.AssemblyQualifiedName, StringComparison.Ordinal);

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

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types;
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] != null && string.Equals(types[i].FullName, typeName, StringComparison.Ordinal) &&
                        types[i].IsEnum)
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

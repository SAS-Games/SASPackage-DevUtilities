using System;
using System.Collections.Generic;
using System.Globalization;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteComponentInspectorView
    {
        private readonly HashSet<long> _expandedComponents = new();
        private readonly Dictionary<string, string> _editValues = new();
        private long _inspectionRevision = long.MinValue;
        private int _sessionGeneration = int.MinValue;

        public void Draw(RemoteRuntimeSceneInspectorClient client, RemoteComponentDescriptor[] components)
        {
            SynchronizeSession(client.SessionGeneration);
            SynchronizeInspection(client.InspectionRevision);
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
            else if (TryDrawMetadataControl(member, out string nextValue, out bool changed))
            {
                if (changed)
                    SetMemberValue(client, componentId, member, nextValue.ToString());
            }
            else
            {
                DrawEditableText(client, componentId, member, key);
            }

            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(member.Error))
                EditorGUILayout.HelpBox(member.Error, MessageType.Warning);
        }

        private void SynchronizeSession(int sessionGeneration)
        {
            if (_sessionGeneration == sessionGeneration)
                return;
            _sessionGeneration = sessionGeneration;
            _inspectionRevision = long.MinValue;
            _expandedComponents.Clear();
            _editValues.Clear();
        }

        private void SynchronizeInspection(long inspectionRevision)
        {
            if (_inspectionRevision == inspectionRevision)
                return;

            _editValues.Clear();
            _inspectionRevision = inspectionRevision;
        }

        private static void DrawReadOnlyMember(RemoteMemberDescriptor member)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                if (!TryDrawMetadataControl(member, out _, out _))
                    EditorGUILayout.TextField(member?.Value ?? string.Empty);
            }
        }

        private void DrawEditableText(RemoteRuntimeSceneInspectorClient client, long componentId,
            RemoteMemberDescriptor member, string key)
        {
            if (!_editValues.TryGetValue(key, out string value))
                value = member.Value ?? string.Empty;
            string controlName = "remote-component-value:" + key;
            GUI.SetNextControlName(controlName);
            value = EditorGUILayout.TextField(value);
            _editValues[key] = value;
            bool dirty = value != (member.Value ?? string.Empty);
            bool apply;
            using (new EditorGUI.DisabledScope(!dirty))
                apply = GUILayout.Button("Apply", GUILayout.Width(48f));
            if (dirty && RemoteInspectorInput.IsApplyKey(Event.current,
                    GUI.GetNameOfFocusedControl(), controlName))
            {
                apply = true;
                Event.current.Use();
            }
            if (apply)
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

        private static bool TryDrawMetadataControl(RemoteMemberDescriptor member,
            out string nextValue, out bool changed)
        {
            nextValue = member?.Value ?? string.Empty;
            changed = false;
            if (member == null)
                return false;

            switch (member.ControlKind)
            {
                case RemoteInspectorControlKind.Boolean:
                    if (!bool.TryParse(member.Value, out bool booleanValue))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    bool nextBoolean = EditorGUILayout.Toggle(booleanValue);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = nextBoolean.ToString();
                    return true;

                case RemoteInspectorControlKind.Enum:
                case RemoteInspectorControlKind.Layer:
                case RemoteInspectorControlKind.SortingLayer:
                    return TryDrawOptionPopup(member, out nextValue, out changed);

                case RemoteInspectorControlKind.EnumFlags:
                    return TryDrawFlags(member, out nextValue, out changed);

                case RemoteInspectorControlKind.LayerMask:
                    return TryDrawLayerMask(member, out nextValue, out changed);

                case RemoteInspectorControlKind.Integer:
                    if (!long.TryParse(member.Value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out long integerValue))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    long nextInteger = EditorGUILayout.LongField(integerValue);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = nextInteger.ToString(CultureInfo.InvariantCulture);
                    return true;

                case RemoteInspectorControlKind.Float:
                    if (!double.TryParse(member.Value, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double floatValue))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    double nextFloat = member.HasRange
                        ? EditorGUILayout.Slider((float)floatValue, member.RangeMinimum,
                            member.RangeMaximum)
                        : EditorGUILayout.DoubleField(floatValue);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = nextFloat.ToString("R", CultureInfo.InvariantCulture);
                    return true;

                case RemoteInspectorControlKind.Vector2:
                    if (!TryParseFloats(member.Value, 2, out float[] vector2Values))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Vector2 vector2 = EditorGUI.Vector2Field(EditorGUILayout.GetControlRect(),
                        GUIContent.none, new Vector2(vector2Values[0], vector2Values[1]));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(vector2.x, vector2.y);
                    return true;

                case RemoteInspectorControlKind.Vector2Int:
                    if (!TryParseInts(member.Value, 2, out int[] vector2IntValues))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Vector2Int vector2Int = EditorGUI.Vector2IntField(EditorGUILayout.GetControlRect(),
                        GUIContent.none, new Vector2Int(vector2IntValues[0], vector2IntValues[1]));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(vector2Int.x, vector2Int.y);
                    return true;

                case RemoteInspectorControlKind.Vector3:
                case RemoteInspectorControlKind.QuaternionEuler:
                    if (!TryParseFloats(member.Value, 3, out float[] vector3Values))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Vector3 vector3 = EditorGUI.Vector3Field(EditorGUILayout.GetControlRect(),
                        GUIContent.none, new Vector3(vector3Values[0], vector3Values[1], vector3Values[2]));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(vector3.x, vector3.y, vector3.z);
                    return true;

                case RemoteInspectorControlKind.Vector3Int:
                    if (!TryParseInts(member.Value, 3, out int[] vector3IntValues))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Vector3Int vector3Int = EditorGUI.Vector3IntField(EditorGUILayout.GetControlRect(),
                        GUIContent.none, new Vector3Int(vector3IntValues[0], vector3IntValues[1],
                            vector3IntValues[2]));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(vector3Int.x, vector3Int.y, vector3Int.z);
                    return true;

                case RemoteInspectorControlKind.Vector4:
                    if (!TryParseFloats(member.Value, 4, out float[] vector4Values))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Vector4 vector4 = EditorGUI.Vector4Field(EditorGUILayout.GetControlRect(),
                        GUIContent.none, new Vector4(vector4Values[0], vector4Values[1],
                            vector4Values[2], vector4Values[3]));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(vector4.x, vector4.y, vector4.z, vector4.w);
                    return true;

                case RemoteInspectorControlKind.Color:
                case RemoteInspectorControlKind.Color32:
                    if (!TryParseFloats(member.Value, 4, out float[] colorValues))
                        return false;
                    Color color = member.ControlKind == RemoteInspectorControlKind.Color32
                        ? new Color32((byte)colorValues[0], (byte)colorValues[1],
                            (byte)colorValues[2], (byte)colorValues[3])
                        : new Color(colorValues[0], colorValues[1], colorValues[2], colorValues[3]);
                    EditorGUI.BeginChangeCheck();
                    Color nextColor = EditorGUI.ColorField(EditorGUILayout.GetControlRect(),
                        GUIContent.none, color, true, true, true);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = member.ControlKind == RemoteInspectorControlKind.Color32
                        ? Join(ToByte(nextColor.r), ToByte(nextColor.g), ToByte(nextColor.b),
                            ToByte(nextColor.a))
                        : Join(nextColor.r, nextColor.g, nextColor.b, nextColor.a);
                    return true;

                case RemoteInspectorControlKind.Rect:
                    if (!TryParseFloats(member.Value, 4, out float[] rectValues))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Rect rect = EditorGUI.RectField(EditorGUILayout.GetControlRect(), GUIContent.none,
                        new Rect(rectValues[0], rectValues[1], rectValues[2], rectValues[3]));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(rect.x, rect.y, rect.width, rect.height);
                    return true;

                case RemoteInspectorControlKind.Bounds:
                    if (!TryParseFloats(member.Value, 6, out float[] boundsValues))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Bounds bounds = EditorGUI.BoundsField(EditorGUILayout.GetControlRect(false,
                            GetControlHeight(RemoteInspectorControlKind.Bounds)),
                        GUIContent.none, new Bounds(
                            new Vector3(boundsValues[0], boundsValues[1], boundsValues[2]),
                            new Vector3(boundsValues[3], boundsValues[4], boundsValues[5])));
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(bounds.center.x, bounds.center.y, bounds.center.z,
                        bounds.size.x, bounds.size.y, bounds.size.z);
                    return true;

                default:
                    return false;
            }
        }

        internal static float GetControlHeight(RemoteInspectorControlKind controlKind)
        {
            if (controlKind == RemoteInspectorControlKind.Bounds)
            {
                return EditorGUIUtility.singleLineHeight * 2f +
                       EditorGUIUtility.standardVerticalSpacing;
            }

            return EditorGUIUtility.singleLineHeight;
        }

        private static bool TryDrawOptionPopup(RemoteMemberDescriptor member, out string nextValue,
            out bool changed)
        {
            nextValue = member.Value ?? string.Empty;
            changed = false;
            RemoteInspectorOption[] options = member.Options ?? Array.Empty<RemoteInspectorOption>();
            if (options.Length == 0)
                return false;

            var labels = new string[options.Length + 1];
            int selectedIndex = -1;
            for (int i = 0; i < options.Length; i++)
            {
                labels[i] = options[i].Label ?? options[i].Value ?? string.Empty;
                if (string.Equals(options[i].Value, member.Value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(options[i].Label, member.Value, StringComparison.OrdinalIgnoreCase))
                    selectedIndex = i;
            }

            int visibleCount = options.Length;
            if (selectedIndex < 0)
            {
                selectedIndex = visibleCount;
                labels[visibleCount++] = member.Value ?? "<unknown>";
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup(selectedIndex,
                visibleCount == labels.Length ? labels : Copy(labels, visibleCount));
            changed = EditorGUI.EndChangeCheck();
            if (nextIndex >= 0 && nextIndex < options.Length)
                nextValue = options[nextIndex].Value;
            return true;
        }

        private static bool TryDrawFlags(RemoteMemberDescriptor member, out string nextValue,
            out bool changed)
        {
            nextValue = member.Value ?? string.Empty;
            changed = false;
            RemoteInspectorOption[] source = member.Options ?? Array.Empty<RemoteInspectorOption>();
            var flags = new List<RemoteInspectorOption>();
            long currentNumeric = 0;
            bool currentKnown = TryResolveOptionNumeric(source, member.Value, out currentNumeric);
            RemoteInspectorOption zero = null;
            foreach (RemoteInspectorOption option in source)
            {
                if (!option.HasNumericValue)
                    continue;
                if (option.NumericValue == 0)
                {
                    zero = option;
                    continue;
                }
                ulong numeric = unchecked((ulong)option.NumericValue);
                if ((numeric & (numeric - 1)) == 0 && flags.Count < 31)
                    flags.Add(option);
            }

            if (!currentKnown || flags.Count == 0)
                return TryDrawOptionPopup(member, out nextValue, out changed);

            var labels = new string[flags.Count];
            int currentMask = 0;
            for (int i = 0; i < flags.Count; i++)
            {
                labels[i] = flags[i].Label ?? flags[i].Value ?? string.Empty;
                if ((currentNumeric & flags[i].NumericValue) == flags[i].NumericValue)
                    currentMask |= 1 << i;
            }

            EditorGUI.BeginChangeCheck();
            int nextMask = EditorGUILayout.MaskField(currentMask, labels);
            changed = EditorGUI.EndChangeCheck();
            if (!changed)
                return true;

            var selected = new List<string>();
            for (int i = 0; i < flags.Count; i++)
            {
                if ((nextMask & (1 << i)) != 0)
                    selected.Add(flags[i].Value);
            }
            nextValue = selected.Count == 0 ? zero?.Value ?? "0" : string.Join(", ", selected);
            return true;
        }

        private static bool TryDrawLayerMask(RemoteMemberDescriptor member, out string nextValue,
            out bool changed)
        {
            nextValue = member.Value ?? string.Empty;
            changed = false;
            if (!int.TryParse(member.Value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int currentMask))
                return false;
            RemoteInspectorOption[] options = member.Options ?? Array.Empty<RemoteInspectorOption>();
            if (options.Length != 32)
                return false;
            var labels = new string[32];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = options[i].Label ?? "Layer " + i;
            EditorGUI.BeginChangeCheck();
            int nextMask = EditorGUILayout.MaskField(currentMask, labels);
            changed = EditorGUI.EndChangeCheck();
            nextValue = nextMask.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        private static bool TryResolveOptionNumeric(RemoteInspectorOption[] options, string value,
            out long numeric)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
                return true;
            numeric = 0;
            string[] names = (value ?? string.Empty).Split(',');
            foreach (string suppliedName in names)
            {
                bool found = false;
                foreach (RemoteInspectorOption option in options)
                {
                    if (!option.HasNumericValue ||
                        !string.Equals(option.Value, suppliedName.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    numeric |= option.NumericValue;
                    found = true;
                    break;
                }
                if (!found)
                    return false;
            }
            return names.Length > 0;
        }

        private static bool TryParseFloats(string value, int count, out float[] values)
        {
            string[] parts = (value ?? string.Empty).Split(',');
            values = new float[count];
            if (parts.Length != count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }
            return true;
        }

        private static bool TryParseInts(string value, int count, out int[] values)
        {
            string[] parts = (value ?? string.Empty).Split(',');
            values = new int[count];
            if (parts.Length != count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }
            return true;
        }

        private static string Join(params object[] values) => string.Join(", ",
            Array.ConvertAll(values, value => Convert.ToString(value, CultureInfo.InvariantCulture)));

        private static byte ToByte(float value) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * byte.MaxValue);

        private static string[] Copy(string[] source, int count)
        {
            var result = new string[count];
            Array.Copy(source, result, count);
            return result;
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

        private static string GetMemberLabel(RemoteMemberDescriptor member)
        {
            string label = member?.DisplayName ?? member?.Name ?? string.Empty;
            string normalized = label.Replace(" ", string.Empty);
            return string.Equals(normalized, "SortingLayerID", StringComparison.OrdinalIgnoreCase)
                ? "Sorting Layer"
                : label;
        }

    }
}

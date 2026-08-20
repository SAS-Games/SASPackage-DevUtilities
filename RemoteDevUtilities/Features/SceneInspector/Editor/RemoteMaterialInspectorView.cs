using System;
using System.Collections.Generic;
using System.Globalization;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteMaterialInspectorView
    {
        private enum ShaderPropertyType
        {
            Float,
            Range,
            Integer,
            Color,
            Vector,
            Texture,
            Unsupported
        }

        private static readonly string[] MaterialScopes =
        {
            "Selected Renderer",
            "Material Instance",
            "Shared Material",
            "Global Shader Property"
        };

        private readonly HashSet<long> _expandedRenderers = new();
        private readonly HashSet<string> _expandedSlots = new();
        private readonly Dictionary<string, string> _editValues = new();
        private readonly Dictionary<string, int> _materialScopes = new();
        private long _inspectionRevision = long.MinValue;
        private int _sessionGeneration = int.MinValue;
        private string _shaderSearch = string.Empty;

        public void Draw(RemoteRuntimeSceneInspectorClient client, RemoteMaterialShaderSection section)
        {
            SynchronizeSession(client.SessionGeneration);
            SynchronizeInspection(client.InspectionRevision);
            if (section?.Renderers == null || section.Renderers.Length == 0)
                return;

            EditorGUILayout.Space(5f);
            DrawHeader(section);

            foreach (RemoteRendererMaterialDescriptor renderer in section.Renderers)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool rendererExpanded = _expandedRenderers.Contains(renderer.RendererId);
                bool nextRenderer = EditorGUILayout.Foldout(rendererExpanded, $"{renderer.RendererName} " + $"({RemoteInspectorFormatting.ShortTypeName(renderer.RendererType)})", true);
                RemoteInspectorFormatting.SetExpanded(_expandedRenderers, renderer.RendererId, nextRenderer);
                if (nextRenderer)
                    DrawMaterialSlots(client, renderer);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawHeader(RemoteMaterialShaderSection section)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(section.DisplayName ?? "Materials & Shaders", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _shaderSearch = EditorGUILayout.TextField(_shaderSearch, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(190f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMaterialSlots(RemoteRuntimeSceneInspectorClient client, RemoteRendererMaterialDescriptor renderer)
        {
            foreach (RemoteMaterialSlotDescriptor slot in renderer.MaterialSlots ?? Array.Empty<RemoteMaterialSlotDescriptor>())
            {
                string slotKey = $"{renderer.RendererId}:{slot.MaterialIndex}";
                bool expanded = _expandedSlots.Contains(slotKey);
                bool next = EditorGUILayout.Foldout(expanded, $"Slot {slot.MaterialIndex}: {slot.MaterialName ?? "<null>"}", true);
                RemoteInspectorFormatting.SetExpanded(_expandedSlots, slotKey, next);
                if (!next)
                    continue;

                EditorGUI.indentLevel++;
                DrawSlotMetadata(slot, slotKey, out int scope);
                DrawShaderProperties(client, renderer.RendererId, slot, scope);
                DrawRestoreButton(client, renderer.RendererId, slot, scope);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSlotMetadata(RemoteMaterialSlotDescriptor slot, string slotKey, out int scope)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Shader", slot.ShaderName ?? "<missing>");
                EditorGUILayout.IntField("Render Queue", slot.RenderQueue);
                EditorGUILayout.Toggle("GPU Instancing", slot.EnableInstancing);
            }

            if (!_materialScopes.TryGetValue(slotKey, out scope))
                scope = FirstWritableScope(slot);
            scope = EditorGUILayout.Popup("Edit Scope", scope, MaterialScopes);
            _materialScopes[slotKey] = scope;

            RemoteMaterialScopeState scopeState = GetScope(slot, scope);
            if (scopeState == null)
                EditorGUILayout.HelpBox("The Player did not provide state for this material scope.",
                    MessageType.Error);
            else if (scopeState.ReadOnly)
                EditorGUILayout.HelpBox("This material scope is read-only in the Player settings.",
                    MessageType.Info);

            if (scope == 2 || scope == 3)
            {
                EditorGUILayout.HelpBox(scope == 2 ? "Shared Material changes may affect multiple renderers." : "Global shader changes may affect multiple materials and shaders.", MessageType.Warning);
            }
        }

        private void DrawShaderProperties(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, int scope)
        {
            foreach (RemoteShaderPropertyView property in slot.Properties ?? Array.Empty<RemoteShaderPropertyView>())
            {
                if (MatchesShaderSearch(property))
                    DrawShaderProperty(client, rendererId, slot, property, scope);
            }

            if (slot.PropertyLimitReached)
            {
                EditorGUILayout.HelpBox($"Showing a limited set of {slot.TotalPropertyCount} shader properties.", MessageType.Info);
            }
        }

        private static void DrawRestoreButton(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, int scope)
        {
            RemoteMaterialScopeState scopeState = GetScope(slot, scope);
            using (new EditorGUI.DisabledScope(scopeState == null || scopeState.ReadOnly ||
                                                !scopeState.HasInspectorOverrides))
            {
                if (!GUILayout.Button("Restore Scope"))
                    return;
            }

            client.Execute(new RemoteSceneInspectorCommandRequest
            {
                Kind = RemoteSceneInspectorCommandKind.RestoreMaterial,
                RendererId = rendererId,
                MaterialIndex = slot.MaterialIndex,
                MaterialScope = scope
            });
        }

        private void DrawShaderProperty(RemoteRuntimeSceneInspectorClient client, long rendererId, RemoteMaterialSlotDescriptor slot, RemoteShaderPropertyView property, int scope)
        {
            string key = $"shader:{rendererId}:{slot.MaterialIndex}:{property.PropertyId}:{scope}";
            RemoteShaderPropertyScopeView scopeView = GetScope(property, scope);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(property.DisplayName ?? property.Name, GUILayout.Width(155f));

            if (scopeView == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("<scope unavailable>");
            }
            else if (scopeView.ReadOnly)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    if (!TryDrawTypedShaderValue(property, scopeView.Value, out _, out _))
                        EditorGUILayout.TextField(scopeView.Value ?? string.Empty);
                }
            }
            else
            {
                if (TryDrawTypedShaderValue(property, scopeView.Value, out string nextValue,
                        out bool changed))
                {
                    if (changed)
                        SetShaderProperty(client, rendererId, slot.MaterialIndex, property.PropertyId,
                            scope, nextValue);
                }
                else
                {
                    DrawEditableShaderProperty(client, rendererId, slot, property, scope,
                        scopeView.Value, key);
                }
            }

            DrawResetButton(client, rendererId, slot, property, scope, scopeView);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(property.Name + "  •  " +
                                       (scopeView?.ValueSource ?? "Scope unavailable"),
                EditorStyles.miniLabel);
        }

        private void DrawEditableShaderProperty(RemoteRuntimeSceneInspectorClient client,
            long rendererId, RemoteMaterialSlotDescriptor slot, RemoteShaderPropertyView property,
            int scope, string authoritativeValue, string key)
        {
            if (!_editValues.TryGetValue(key, out string value))
                value = authoritativeValue ?? string.Empty;
            string controlName = "remote-shader-value:" + key;
            GUI.SetNextControlName(controlName);
            value = EditorGUILayout.TextField(value);
            _editValues[key] = value;
            bool dirty = value != (authoritativeValue ?? string.Empty);
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
                SetShaderProperty(client, rendererId, slot.MaterialIndex, property.PropertyId,
                    scope, value);
            }
        }

        private void SynchronizeSession(int sessionGeneration)
        {
            if (_sessionGeneration == sessionGeneration)
                return;
            _sessionGeneration = sessionGeneration;
            _inspectionRevision = long.MinValue;
            _expandedRenderers.Clear();
            _expandedSlots.Clear();
            _editValues.Clear();
            _materialScopes.Clear();
            _shaderSearch = string.Empty;
        }

        private static bool TryDrawTypedShaderValue(RemoteShaderPropertyView property,
            string currentValue,
            out string nextValue, out bool changed)
        {
            nextValue = currentValue ?? string.Empty;
            changed = false;
            if (property == null)
                return false;

            switch ((ShaderPropertyType)property.Type)
            {
                case ShaderPropertyType.Float:
                case ShaderPropertyType.Range:
                    if (!float.TryParse(currentValue, NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float scalar))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    float nextScalar = property.Type == (int)ShaderPropertyType.Range
                        ? EditorGUILayout.Slider(scalar, property.RangeMinimum,
                            property.RangeMaximum)
                        : EditorGUILayout.FloatField(scalar);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = nextScalar.ToString("R", CultureInfo.InvariantCulture);
                    return true;

                case ShaderPropertyType.Integer:
                    if (!int.TryParse(currentValue, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int integer))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    int nextInteger = EditorGUILayout.IntField(integer);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = nextInteger.ToString(CultureInfo.InvariantCulture);
                    return true;

                case ShaderPropertyType.Color:
                    if (!TryParseVector(currentValue, out Vector4 colorValues))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Color nextColor = EditorGUI.ColorField(EditorGUILayout.GetControlRect(),
                        GUIContent.none,
                        new Color(colorValues.x, colorValues.y, colorValues.z, colorValues.w),
                        true, true, true);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(nextColor.r, nextColor.g, nextColor.b, nextColor.a);
                    return true;

                case ShaderPropertyType.Vector:
                    if (!TryParseVector(currentValue, out Vector4 vector))
                        return false;
                    EditorGUI.BeginChangeCheck();
                    Vector4 nextVector = EditorGUI.Vector4Field(EditorGUILayout.GetControlRect(),
                        GUIContent.none, vector);
                    changed = EditorGUI.EndChangeCheck();
                    nextValue = Join(nextVector.x, nextVector.y, nextVector.z, nextVector.w);
                    return true;

                default:
                    // Textures stay descriptive/read-only unless the existing text command is
                    // used to clear them. Assignment needs the planned runtime texture registry.
                    return false;
            }
        }

        private static void SetShaderProperty(RemoteRuntimeSceneInspectorClient client,
            long rendererId, int materialIndex, int propertyId, int scope, string value)
        {
            client.Execute(new RemoteSceneInspectorCommandRequest
            {
                Kind = RemoteSceneInspectorCommandKind.SetShaderProperty,
                RendererId = rendererId,
                MaterialIndex = materialIndex,
                PropertyId = propertyId,
                MaterialScope = scope,
                Value = value
            });
        }

        private static bool TryParseVector(string value, out Vector4 vector)
        {
            vector = default;
            string[] parts = (value ?? string.Empty).Split(',');
            if (parts.Length != 4)
                return false;
            var values = new float[4];
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }
            vector = new Vector4(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static string Join(params float[] values) => string.Join(", ",
            Array.ConvertAll(values, value => value.ToString("R", CultureInfo.InvariantCulture)));

        private void SynchronizeInspection(long inspectionRevision)
        {
            if (_inspectionRevision == inspectionRevision)
                return;

            _editValues.Clear();
            _inspectionRevision = inspectionRevision;
        }

        private static void DrawResetButton(RemoteRuntimeSceneInspectorClient client,
            long rendererId, RemoteMaterialSlotDescriptor slot, RemoteShaderPropertyView property,
            int scope, RemoteShaderPropertyScopeView scopeView)
        {
            using (new EditorGUI.DisabledScope(scopeView == null || scopeView.ReadOnly ||
                                                !scopeView.HasInspectorOverride))
            {
                if (!GUILayout.Button("Reset", GUILayout.Width(48f)))
                    return;

                client.Execute(new RemoteSceneInspectorCommandRequest
                {
                    Kind = RemoteSceneInspectorCommandKind.RestoreShaderProperty,
                    RendererId = rendererId,
                    MaterialIndex = slot.MaterialIndex,
                    PropertyId = property.PropertyId,
                    MaterialScope = scope
                });
            }
        }

        private static RemoteShaderPropertyScopeView GetScope(RemoteShaderPropertyView property,
            int scope)
        {
            foreach (RemoteShaderPropertyScopeView candidate in
                     property?.Scopes ?? Array.Empty<RemoteShaderPropertyScopeView>())
            {
                if (candidate != null && candidate.Scope == scope)
                    return candidate;
            }
            return null;
        }

        private static RemoteMaterialScopeState GetScope(RemoteMaterialSlotDescriptor slot,
            int scope)
        {
            foreach (RemoteMaterialScopeState candidate in
                     slot?.Scopes ?? Array.Empty<RemoteMaterialScopeState>())
            {
                if (candidate != null && candidate.Scope == scope)
                    return candidate;
            }
            return null;
        }

        private static int FirstWritableScope(RemoteMaterialSlotDescriptor slot)
        {
            for (int scope = 0; scope < MaterialScopes.Length; scope++)
            {
                RemoteMaterialScopeState state = GetScope(slot, scope);
                if (state != null && !state.ReadOnly)
                    return scope;
            }
            return 0;
        }

        private bool MatchesShaderSearch(RemoteShaderPropertyView property)
        {
            if (string.IsNullOrWhiteSpace(_shaderSearch))
                return true;

            return (property.Name?.IndexOf(_shaderSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (property.DisplayName?.IndexOf(_shaderSearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }
    }
}

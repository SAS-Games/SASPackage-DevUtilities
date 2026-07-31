using System;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector
{
    /// <summary>IMGUI renderer for the keyboard-navigable material/shader inspector extension.</summary>
    internal sealed class RuntimeMaterialShaderInspectorView
    {
        private const string ShaderEditValueControlName = "RuntimeSceneInspector.ShaderEditValue";
        private readonly RuntimeSceneInspectorController _controller;
        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly RuntimeSceneInspectorTheme _theme;

        internal RuntimeMaterialShaderInspectorView(RuntimeSceneInspectorController controller, RuntimeSceneInspectorSettings settings, RuntimeSceneInspectorTheme theme)
        {
            _controller = controller;
            _settings = settings;
            _theme = theme;
        }

        internal void Draw(RuntimeMaterialShaderSection section, ref int rowIndex)
        {
            if (section?.Renderers == null || section.Renderers.Count == 0)
                return;

            GUILayout.Space(10f);
            int sectionRowIndex = rowIndex++;
            bool expanded = _controller.MaterialsExpanded;
            GUIStyle sectionStyle = RowStyle(sectionRowIndex);
            if (GUILayout.Button((expanded ? "\u25BC " : "\u25B6 ") + section.DisplayName.ToUpperInvariant() + "   [RUNTIME]", sectionStyle, GUILayout.Height(24f)))
            {
                _controller.ToggleMaterialsFoldout(sectionRowIndex);
                expanded = !expanded;
            }

            RevealCursor(sectionRowIndex);

            if (!expanded)
                return;

            DrawPropertySearch();
            foreach (RuntimeRendererMaterialDescriptor renderer in section.Renderers)
                DrawRenderer(renderer, ref rowIndex);
        }

        private void DrawPropertySearch()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("PROPERTY SEARCH", _theme.Muted, GUILayout.Width(118f));
            string current = _controller.ShaderPropertySearch;
            string next = GUILayout.TextField(current, _theme.SearchField, GUILayout.Height(24f));
            if (!string.Equals(next, current, StringComparison.Ordinal))
                _controller.SetShaderPropertySearch(next);
            if (!string.IsNullOrEmpty(next) && GUILayout.Button("CLEAR", _theme.Button, GUILayout.Width(52f), GUILayout.Height(24f)))
                _controller.SetShaderPropertySearch(string.Empty);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private void DrawRenderer(RuntimeRendererMaterialDescriptor renderer, ref int rowIndex)
        {
            bool expanded = _controller.IsMaterialRendererExpanded(renderer.RendererId);
            GUILayout.BeginVertical(_theme.Component);
            int rendererRowIndex = rowIndex++;
            if (GUILayout.Button((expanded ? "\u25BC " : "\u25B6 ") + ShortTypeName(renderer.RendererType) + "  " + renderer.RendererName, RowStyle(rendererRowIndex), GUILayout.Height(24f)))
            {
                _controller.ToggleMaterialRendererFoldout(renderer.RendererId, rendererRowIndex);
                expanded = !expanded;
            }

            RevealCursor(rendererRowIndex);

            if (expanded)
            {
                foreach (RuntimeMaterialSlotDescriptor slot in renderer.MaterialSlots ?? Array.Empty<RuntimeMaterialSlotDescriptor>())
                    DrawSlot(renderer, slot, ref rowIndex);
            }

            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void DrawSlot(RuntimeRendererMaterialDescriptor renderer, RuntimeMaterialSlotDescriptor slot, ref int rowIndex)
        {
            bool expanded = _controller.IsMaterialSlotExpanded(renderer.RendererId, slot.MaterialIndex);
            GUILayout.BeginVertical(_theme.Summary);
            int slotRowIndex = rowIndex++;
            string materialLabel = slot.MissingMaterial ? "<empty material>" : slot.MaterialName;
            if (GUILayout.Button((expanded ? "\u25BC " : "\u25B6 ") + $"Slot {slot.MaterialIndex}: " + materialLabel, RowStyle(slotRowIndex), GUILayout.Height(23f)))
            {
                _controller.ToggleMaterialSlotFoldout(renderer.RendererId, slot.MaterialIndex, slotRowIndex);
                expanded = !expanded;
            }

            RevealCursor(slotRowIndex);

            if (!expanded)
            {
                GUILayout.EndVertical();
                return;
            }

            if (slot.MissingMaterial)
            {
                GUILayout.Label("This material slot is empty.", _theme.Muted);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label($"Shader: {slot.ShaderName}   Material ID: {slot.MaterialInstanceId}", _theme.Muted);
            GUILayout.Label($"Queue: {slot.RenderQueue}   Instancing: {slot.EnableInstancing}   " + $"Properties: {slot.TotalPropertyCount}" + (slot.IsInspectorMaterialInstance ? "   INSPECTOR INSTANCE" : string.Empty), _theme.Muted);

            if (slot.MissingShader)
            {
                GUILayout.Label("The shader is missing.", _theme.Message);
                GUILayout.EndVertical();
                return;
            }

            RuntimeMaterialEditScope scope = _controller.GetMaterialScope(renderer.RendererId, slot.MaterialIndex);
            DrawScopeSelector(renderer.RendererId, slot.MaterialIndex, ref scope);
            if (scope == RuntimeMaterialEditScope.SharedMaterial)
                GUILayout.Label("Shared Material: changes may affect multiple objects using this material.", _theme.Message);
            else if (scope == RuntimeMaterialEditScope.GlobalShaderProperty)
                GUILayout.Label("Global Shader Property: changes may affect multiple shaders and materials.", _theme.Message);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("RESTORE SCOPE", _theme.WarningButton, GUILayout.Width(112f), GUILayout.Height(23f)))
                _controller.RestoreShaderMaterial(renderer.RendererId, slot.MaterialIndex, scope);
            GUILayout.EndHorizontal();

            int visibleCount = 0;
            foreach (RuntimeShaderPropertyView property in slot.Properties ?? Array.Empty<RuntimeShaderPropertyView>())
            {
                if (!_controller.MatchesShaderProperty(property.Property))
                    continue;
                visibleCount++;
                DrawProperty(renderer.RendererId, slot, scope, property, rowIndex++);
            }

            if (visibleCount == 0)
                GUILayout.Label(string.IsNullOrWhiteSpace(_controller.ShaderPropertySearch) ? "No visible shader properties." : "No shader properties match the search.", _theme.Muted);
            if (slot.PropertyLimitReached)
                GUILayout.Label($"Showing the first {_settings.MaxVisibleShaderProperties} permitted properties.", _theme.Message);

            GUILayout.EndVertical();
        }

        private void DrawScopeSelector(RuntimeObjectId rendererId, int materialIndex, ref RuntimeMaterialEditScope scope)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("EDIT SCOPE", _theme.Muted, GUILayout.Width(82f));
            DrawScopeButton(rendererId, materialIndex, RuntimeMaterialEditScope.RendererPropertyBlock, "RENDERER", ref scope);
            DrawScopeButton(rendererId, materialIndex, RuntimeMaterialEditScope.MaterialInstance, "INSTANCE", ref scope);
            DrawScopeButton(rendererId, materialIndex, RuntimeMaterialEditScope.SharedMaterial, "SHARED", ref scope);
            DrawScopeButton(rendererId, materialIndex, RuntimeMaterialEditScope.GlobalShaderProperty, "GLOBAL", ref scope);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawScopeButton(RuntimeObjectId rendererId, int materialIndex, RuntimeMaterialEditScope candidate, string label, ref RuntimeMaterialEditScope current)
        {
            if (!_controller.IsMaterialScopeAllowed(candidate))
                return;

            GUIStyle style = candidate == current ? _theme.PrimaryButton : _theme.Button;
            if (!GUILayout.Button(label, style, GUILayout.Width(70f), GUILayout.Height(22f)))
                return;

            current = candidate;
            _controller.SetMaterialScope(rendererId, materialIndex, candidate);
        }

        private void DrawProperty(RuntimeObjectId rendererId, RuntimeMaterialSlotDescriptor slot, RuntimeMaterialEditScope scope, RuntimeShaderPropertyView view, int propertyRowIndex)
        {
            RuntimeShaderPropertyDescriptor property = view.Property;
            bool isEditing = _controller.IsEditingShaderProperty(rendererId, slot.MaterialIndex, property.PropertyId);
            bool canEdit = !view.ReadOnly && _controller.IsMaterialScopeAllowed(scope);

            GUILayout.BeginVertical(RowStyle(propertyRowIndex));
            GUILayout.BeginHorizontal();
            string flags = property.IsPerRendererData ? "  [Per Renderer]" : property.IsMainColor ? "  [Main Color]" : property.IsMainTexture ? "  [Main Texture]" : property.IsHdr ? "  [HDR]" : string.Empty;
            GUILayout.Label(property.DisplayName + flags, _theme.Body, GUILayout.Width(210f));
            GUILayout.Label(property.Name, _theme.Muted, GUILayout.Width(130f));

            if (isEditing)
            {
                GUI.SetNextControlName(ShaderEditValueControlName);
                _controller.EditValue = GUILayout.TextField(_controller.EditValue, _theme.ValueField, GUILayout.Height(22f));
                if (_controller.FocusEditField)
                {
                    GUI.FocusControl(ShaderEditValueControlName);
                    if (Event.current.type == EventType.Repaint)
                        _controller.FocusEditField = false;
                }

                if (GUILayout.Button("SAVE", _theme.PrimaryButton, GUILayout.Width(48f), GUILayout.Height(22f)))
                    _controller.CommitEdit();
                if (GUILayout.Button("X", _theme.Button, GUILayout.Width(24f), GUILayout.Height(22f)))
                    _controller.CancelEdit();
            }
            else
            {
                GUILayout.Label(view.Value, _theme.Body);
                if (canEdit && GUILayout.Button("EDIT", _theme.PrimaryButton, GUILayout.Width(48f), GUILayout.Height(22f)))
                {
                    _controller.BeginShaderEditFromView(rendererId, slot.MaterialIndex, view, propertyRowIndex);
                }
            }

            if (canEdit && !isEditing && GUILayout.Button("RESET", _theme.Button, GUILayout.Width(50f), GUILayout.Height(22f)))
                _controller.RestoreShaderProperty(rendererId, slot.MaterialIndex, property.PropertyId, scope);

            GUILayout.EndHorizontal();
            string range = property.Type == RuntimeShaderPropertyType.Range ? $"   Range {property.RangeMinimum:G5} to {property.RangeMaximum:G5}" : string.Empty;
            GUILayout.Label($"Type: {property.Type}   Source: {view.ValueSource}{range}", _theme.Muted);
            GUILayout.EndVertical();
            RevealCursor(propertyRowIndex);
        }

        private GUIStyle RowStyle(int rowIndex) => _controller.IsInspectorFocused && _controller.InspectorCursor == rowIndex ? _theme.SelectedRow : _theme.Row;

        private void RevealCursor(int rowIndex)
        {
            if (!_controller.RevealInspectorCursor || !_controller.IsInspectorFocused || rowIndex != _controller.InspectorCursor || Event.current.type != EventType.Repaint)
                return;

            GUI.ScrollTo(GUILayoutUtility.GetLastRect());
            _controller.RevealInspectorCursor = false;
        }

        private static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "Renderer";
            int separator = typeName.LastIndexOf('.');
            return separator >= 0 ? typeName.Substring(separator + 1) : typeName;
        }
    }
}

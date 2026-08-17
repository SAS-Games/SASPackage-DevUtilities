using System;
using System.Collections.Generic;
using System.Linq;
using HP.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector
{
    internal sealed class RuntimeSceneInspectorDetailsController
    {
        private readonly IRuntimeSceneInspector _service;
        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly HashSet<long> _expandedComponents = new();
        private readonly HashSet<long> _knownMaterialRenderers = new();
        private readonly HashSet<long> _expandedMaterialRenderers = new();
        private readonly HashSet<MaterialSlotKey> _knownMaterialSlots = new();
        private readonly HashSet<MaterialSlotKey> _expandedMaterialSlots = new();
        private readonly Dictionary<MaterialSlotKey, RuntimeMaterialEditScope> _materialScopes = new();
        private RuntimeObjectDetails _details;
        private RuntimeMemberDescriptor _editingMember;
        private RuntimeComponentDescriptor _editingComponent;
        private RuntimeShaderPropertyView _editingShaderProperty;
        private RuntimeObjectId _editingRendererId;
        private int _editingMaterialIndex = -1;
        private string _shaderPropertySearch = string.Empty;
        private string _editValue = string.Empty;
        private int _cursor;
        private bool _materialsExpanded = true;

        internal RuntimeSceneInspectorDetailsController(IRuntimeSceneInspector service, RuntimeSceneInspectorSettings settings)
        {
            _service = service;
            _settings = settings;
        }

        internal RuntimeObjectDetails Details => _details;
        internal HashSet<long> ExpandedComponents => _expandedComponents;
        internal int Cursor => _cursor;
        internal bool IsEditing => IsEditingMemberValue || IsEditingShaderValue;
        internal bool FocusEditField { get; set; }
        internal bool RevealCursor { get; set; }
        internal bool MaterialsExpanded => _materialsExpanded;
        internal string ShaderPropertySearch => _shaderPropertySearch;

        private bool IsEditingMemberValue => _editingComponent != null && _editingMember != null;
        private bool IsEditingShaderValue => _editingRendererId.IsValid && _editingShaderProperty != null;

        internal string EditValue
        {
            get => _editValue;
            set => _editValue = value;
        }

        internal string Refresh()
        {
            string selectedRowId = SelectedRowId();
            if (_details != null)
                _details = _service.InspectObject(_details.Id);
            EnsureMaterialFoldoutDefaults();

            if (IsEditing && !EditingTargetExists())
            {
                CancelEdit();
                RestoreCursor(selectedRowId);
                return "The edited value is no longer available.";
            }

            RestoreCursor(selectedRowId);
            return null;
        }

        internal void Select(RuntimeObjectId id)
        {
            if (IsEditing)
                CancelEdit();
            _details = _service.InspectObject(id);
            _cursor = 0;
            _materialsExpanded = true;
            RevealCursor = true;
            if (_details == null)
                return;

            foreach (RuntimeComponentDescriptor component in _details.Components)
            {
                if (HasMembers(component))
                    _expandedComponents.Add(component.Id.Value);
            }

            EnsureMaterialFoldoutDefaults();
        }

        internal void Navigate(RuntimeSceneInspectorNavigationCommand command)
        {
            List<InspectorRow> rows = GetRows();
            if (rows.Count == 0)
                return;

            int previousCursor = _cursor;
            _cursor = Mathf.Clamp(_cursor, 0, rows.Count - 1);
            switch (command)
            {
                case RuntimeSceneInspectorNavigationCommand.Up:
                    _cursor--;
                    break;
                case RuntimeSceneInspectorNavigationCommand.Down:
                    _cursor++;
                    break;
                case RuntimeSceneInspectorNavigationCommand.Home:
                    _cursor = 0;
                    break;
                case RuntimeSceneInspectorNavigationCommand.End:
                    _cursor = rows.Count - 1;
                    break;
                case RuntimeSceneInspectorNavigationCommand.PageUp:
                    _cursor -= 8;
                    break;
                case RuntimeSceneInspectorNavigationCommand.PageDown:
                    _cursor += 8;
                    break;
                case RuntimeSceneInspectorNavigationCommand.Left:
                case RuntimeSceneInspectorNavigationCommand.Right:
                    NavigateFoldout(rows, command);
                    break;
            }

            rows = GetRows();
            _cursor = Mathf.Clamp(_cursor, 0, Mathf.Max(0, rows.Count - 1));
            if (_cursor != previousCursor)
                RevealCursor = true;
        }

        internal RuntimeCommandResult ActivateCurrent(bool confirm, bool space, out bool requiresRefresh)
        {
            requiresRefresh = false;
            List<InspectorRow> rows = GetRows();
            if (rows.Count == 0)
                return null;

            _cursor = Mathf.Clamp(_cursor, 0, rows.Count - 1);
            InspectorRow selected = rows[_cursor];
            switch (selected.Kind)
            {
                case InspectorRowKind.Active:
                    if (!confirm && !space)
                        return null;
                    requiresRefresh = true;
                    return ToggleInspectedObjectActive();
                case InspectorRowKind.Component:
                    if (confirm || space && !selected.Component.HasEnabledState)
                    {
                        ToggleFoldout(selected.Component);
                        return null;
                    }

                    if (!space)
                        return null;
                    requiresRefresh = true;
                    return ToggleComponentEnabled(selected.Component);
                case InspectorRowKind.ComponentMember:
                    if (confirm && !selected.Member.ReadOnly)
                        BeginEdit(selected.Component, selected.Member);
                    return null;
                case InspectorRowKind.MaterialSection:
                case InspectorRowKind.MaterialRenderer:
                case InspectorRowKind.MaterialSlot:
                    if (confirm || space)
                        ToggleRowExpansion(selected);
                    return null;
                case InspectorRowKind.ShaderProperty:
                    if (confirm && CanEditShaderProperty(selected.ShaderProperty, selected.Renderer.RendererId, selected.Slot.MaterialIndex))
                    {
                        BeginShaderEdit(selected.Renderer.RendererId, selected.Slot.MaterialIndex, selected.ShaderProperty);
                    }

                    return null;
                default:
                    return null;
            }
        }

        internal void ToggleFoldout(RuntimeComponentDescriptor component, int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            if (!HasMembers(component))
                return;
            if (!_expandedComponents.Remove(component.Id.Value))
                _expandedComponents.Add(component.Id.Value);
            RevealCursor = true;
        }

        internal void ToggleMaterialsFoldout(int rowIndex)
        {
            _cursor = rowIndex;
            _materialsExpanded = !_materialsExpanded;
            RevealCursor = true;
        }

        internal void ToggleMaterialRendererFoldout(RuntimeObjectId rendererId, int rowIndex)
        {
            _cursor = rowIndex;
            if (!_expandedMaterialRenderers.Remove(rendererId.Value))
                _expandedMaterialRenderers.Add(rendererId.Value);
            RevealCursor = true;
        }

        internal void ToggleMaterialSlotFoldout(RuntimeObjectId rendererId, int materialIndex, int rowIndex)
        {
            _cursor = rowIndex;
            var key = new MaterialSlotKey(rendererId.Value, materialIndex);
            if (!_expandedMaterialSlots.Remove(key))
                _expandedMaterialSlots.Add(key);
            RevealCursor = true;
        }

        internal RuntimeCommandResult ToggleInspectedObjectActive(int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            if (_details == null)
                return RuntimeCommandResult.Fail("No object is being inspected.");
            return _service.Execute(new SetGameObjectActiveCommand { ObjectId = _details.Id, Active = !_details.Active });
        }

        internal RuntimeCommandResult ToggleComponentEnabled(RuntimeComponentDescriptor component, int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            if (IsEditing)
                CancelEdit();
            return _service.Execute(new SetComponentEnabledCommand { ComponentId = component.Id, Enabled = !component.Enabled });
        }

        internal void BeginEdit(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member, int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            ClearEditingTargets();
            _editingComponent = component;
            _editingMember = member;
            _editValue = member.Value;
            FocusEditField = true;
        }

        internal void BeginShaderEdit(RuntimeObjectId rendererId, int materialIndex, RuntimeShaderPropertyView property, int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            if (!CanEditShaderProperty(property, rendererId, materialIndex))
                return;

            ClearEditingTargets();
            _editingRendererId = rendererId;
            _editingMaterialIndex = materialIndex;
            _editingShaderProperty = property;
            _editValue = property.Property.Type == RuntimeShaderPropertyType.Texture ? "null" : property.Value;
            FocusEditField = true;
        }

        internal void CancelEdit()
        {
            ClearEditingTargets();
            FocusEditField = false;
        }

        internal RuntimeCommandResult CommitEdit()
        {
            RuntimeCommandResult result;
            if (IsEditingMemberValue)
            {
                result = _service.Execute(new SetMemberValueCommand
                {
                    ComponentId = _editingComponent.Id,
                    MemberName = _editingMember.Name,
                    Value = _editValue
                });
            }
            else if (IsEditingShaderValue)
            {
                result = _service.Execute(new SetRuntimeShaderPropertyCommand
                {
                    RendererId = _editingRendererId,
                    MaterialIndex = _editingMaterialIndex,
                    PropertyId = _editingShaderProperty.Property.PropertyId,
                    Scope = GetMaterialScope(_editingRendererId, _editingMaterialIndex),
                    Value = _editValue
                });
            }
            else
            {
                CancelEdit();
                return RuntimeCommandResult.Fail("The edited value is no longer available.");
            }

            if (!result.Success)
            {
                FocusEditField = true;
                return result;
            }

            CancelEdit();
            return result;
        }

        internal bool IsEditingMember(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member) => IsEditingMemberValue && component.Id.Equals(_editingComponent.Id) && string.Equals(member.Name, _editingMember.Name, StringComparison.Ordinal);

        internal bool IsEditingShaderProperty(RuntimeObjectId rendererId, int materialIndex, int propertyId) => IsEditingShaderValue && rendererId.Equals(_editingRendererId) && materialIndex == _editingMaterialIndex && propertyId == _editingShaderProperty.Property.PropertyId;

        internal bool IsMaterialRendererExpanded(RuntimeObjectId rendererId) => _expandedMaterialRenderers.Contains(rendererId.Value);

        internal bool IsMaterialSlotExpanded(RuntimeObjectId rendererId, int materialIndex) => _expandedMaterialSlots.Contains(new MaterialSlotKey(rendererId.Value, materialIndex));

        internal RuntimeMaterialEditScope GetMaterialScope(RuntimeObjectId rendererId, int materialIndex)
        {
            var key = new MaterialSlotKey(rendererId.Value, materialIndex);
            if (_materialScopes.TryGetValue(key, out RuntimeMaterialEditScope scope) && ScopeAllowed(scope))
                return scope;

            scope = FirstAllowedScope();
            _materialScopes[key] = scope;
            return scope;
        }

        internal void SetMaterialScope(RuntimeObjectId rendererId, int materialIndex, RuntimeMaterialEditScope scope)
        {
            if (!ScopeAllowed(scope))
                return;
            if (IsEditing)
                CancelEdit();
            _materialScopes[new MaterialSlotKey(rendererId.Value, materialIndex)] = scope;
        }

        internal void SetShaderPropertySearch(string value)
        {
            string next = value ?? string.Empty;
            if (string.Equals(next, _shaderPropertySearch, StringComparison.Ordinal))
                return;

            string selectedRowId = SelectedRowId();
            _shaderPropertySearch = next;
            RestoreCursor(selectedRowId);
        }

        internal bool MatchesShaderProperty(RuntimeShaderPropertyDescriptor property)
        {
            if (property == null)
                return false;
            if (string.IsNullOrWhiteSpace(_shaderPropertySearch))
                return true;
            return (property.Name?.IndexOf(_shaderPropertySearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (property.DisplayName?.IndexOf(_shaderPropertySearch, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        internal bool ScopeAllowed(RuntimeMaterialEditScope scope)
        {
            switch (scope)
            {
                case RuntimeMaterialEditScope.RendererPropertyBlock:
                    return _settings.AllowMaterialPropertyBlockChanges;
                case RuntimeMaterialEditScope.MaterialInstance:
                    return _settings.AllowMaterialInstantiation;
                case RuntimeMaterialEditScope.SharedMaterial:
                    return _settings.AllowSharedMaterialChanges;
                case RuntimeMaterialEditScope.GlobalShaderProperty:
                    return _settings.AllowGlobalShaderChanges;
                default:
                    return false;
            }
        }

        internal static bool HasMembers(RuntimeComponentDescriptor component) => component.Members != null && component.Members.Count > 0;

        private void NavigateFoldout(List<InspectorRow> rows, RuntimeSceneInspectorNavigationCommand command)
        {
            InspectorRow selected = rows[_cursor];
            if (command == RuntimeSceneInspectorNavigationCommand.Left)
            {
                if (IsFoldout(selected) && IsExpanded(selected))
                {
                    SetExpanded(selected, false);
                    RevealCursor = true;
                    return;
                }

                if (string.IsNullOrEmpty(selected.ParentId))
                    return;
                int parentIndex = rows.FindIndex(row => string.Equals(row.Id, selected.ParentId, StringComparison.Ordinal));
                if (parentIndex >= 0)
                {
                    _cursor = parentIndex;
                    RevealCursor = true;
                }

                return;
            }

            if (!IsFoldout(selected))
                return;
            if (!IsExpanded(selected))
            {
                SetExpanded(selected, true);
                RevealCursor = true;
                return;
            }

            if (_cursor + 1 < rows.Count && string.Equals(rows[_cursor + 1].ParentId, selected.Id, StringComparison.Ordinal))
            {
                _cursor++;
                RevealCursor = true;
            }
        }

        private void ToggleRowExpansion(InspectorRow row)
        {
            if (!IsFoldout(row))
                return;
            SetExpanded(row, !IsExpanded(row));
            RevealCursor = true;
        }

        private static bool IsFoldout(InspectorRow row)
        {
            switch (row.Kind)
            {
                case InspectorRowKind.Component:
                    return HasMembers(row.Component);
                case InspectorRowKind.MaterialSection:
                    return row.Section?.Renderers != null && row.Section.Renderers.Count > 0;
                case InspectorRowKind.MaterialRenderer:
                    return row.Renderer?.MaterialSlots != null && row.Renderer.MaterialSlots.Count > 0;
                case InspectorRowKind.MaterialSlot:
                    return true;
                default:
                    return false;
            }
        }

        private bool IsExpanded(InspectorRow row)
        {
            switch (row.Kind)
            {
                case InspectorRowKind.Component:
                    return _expandedComponents.Contains(row.Component.Id.Value);
                case InspectorRowKind.MaterialSection:
                    return _materialsExpanded;
                case InspectorRowKind.MaterialRenderer:
                    return IsMaterialRendererExpanded(row.Renderer.RendererId);
                case InspectorRowKind.MaterialSlot:
                    return IsMaterialSlotExpanded(row.Renderer.RendererId, row.Slot.MaterialIndex);
                default:
                    return false;
            }
        }

        private void SetExpanded(InspectorRow row, bool expanded)
        {
            switch (row.Kind)
            {
                case InspectorRowKind.Component:
                    SetContains(_expandedComponents, row.Component.Id.Value, expanded);
                    break;
                case InspectorRowKind.MaterialSection:
                    _materialsExpanded = expanded;
                    break;
                case InspectorRowKind.MaterialRenderer:
                    SetContains(_expandedMaterialRenderers, row.Renderer.RendererId.Value, expanded);
                    break;
                case InspectorRowKind.MaterialSlot:
                    SetContains(_expandedMaterialSlots, new MaterialSlotKey(row.Renderer.RendererId.Value, row.Slot.MaterialIndex), expanded);
                    break;
            }
        }

        private bool CanEditShaderProperty(RuntimeShaderPropertyView property, RuntimeObjectId rendererId, int materialIndex) => property != null && !property.ReadOnly && ScopeAllowed(GetMaterialScope(rendererId, materialIndex));

        private RuntimeMaterialEditScope FirstAllowedScope()
        {
            if (_settings.AllowMaterialPropertyBlockChanges)
                return RuntimeMaterialEditScope.RendererPropertyBlock;
            if (_settings.AllowMaterialInstantiation)
                return RuntimeMaterialEditScope.MaterialInstance;
            if (_settings.AllowSharedMaterialChanges)
                return RuntimeMaterialEditScope.SharedMaterial;
            return RuntimeMaterialEditScope.GlobalShaderProperty;
        }

        private string SelectedRowId()
        {
            List<InspectorRow> rows = GetRows();
            return _cursor >= 0 && _cursor < rows.Count ? rows[_cursor].Id : null;
        }

        private void RestoreCursor(string selectedRowId)
        {
            List<InspectorRow> rows = GetRows();
            int previousCursor = _cursor;
            int restoredCursor = !string.IsNullOrEmpty(selectedRowId) ? rows.FindIndex(row => string.Equals(row.Id, selectedRowId, StringComparison.Ordinal)) : -1;
            _cursor = restoredCursor >= 0 ? restoredCursor : Mathf.Clamp(previousCursor, 0, Mathf.Max(0, rows.Count - 1));
            if (_cursor != previousCursor || !string.IsNullOrEmpty(selectedRowId) && restoredCursor < 0)
                RevealCursor = true;
        }

        private List<InspectorRow> GetRows()
        {
            var rows = new List<InspectorRow>();
            if (_details == null)
                return rows;

            rows.Add(InspectorRow.Active());
            foreach (RuntimeComponentDescriptor component in _details.Components)
            {
                if (component.Missing)
                    continue;

                InspectorRow componentRow = InspectorRow.ForComponent(component);
                rows.Add(componentRow);
                if (_expandedComponents.Contains(component.Id.Value) && component.Members != null)
                {
                    foreach (RuntimeMemberDescriptor member in component.Members)
                        rows.Add(InspectorRow.ForMember(component, member, componentRow.Id));
                }
            }

            RuntimeMaterialShaderSection section = _details.MaterialsAndShaders;
            if (section?.Renderers == null || section.Renderers.Count == 0)
                return rows;

            InspectorRow sectionRow = InspectorRow.ForMaterialSection(section);
            rows.Add(sectionRow);
            if (!_materialsExpanded)
                return rows;

            foreach (RuntimeRendererMaterialDescriptor renderer in section.Renderers)
            {
                InspectorRow rendererRow = InspectorRow.ForMaterialRenderer(renderer, sectionRow.Id);
                rows.Add(rendererRow);
                if (!IsMaterialRendererExpanded(renderer.RendererId))
                    continue;

                foreach (RuntimeMaterialSlotDescriptor slot in renderer.MaterialSlots ?? Array.Empty<RuntimeMaterialSlotDescriptor>())
                {
                    InspectorRow slotRow = InspectorRow.ForMaterialSlot(renderer, slot, rendererRow.Id);
                    rows.Add(slotRow);
                    if (!IsMaterialSlotExpanded(renderer.RendererId, slot.MaterialIndex))
                        continue;

                    foreach (RuntimeShaderPropertyView property in slot.Properties ?? Array.Empty<RuntimeShaderPropertyView>())
                    {
                        if (MatchesShaderProperty(property.Property))
                            rows.Add(InspectorRow.ForShaderProperty(renderer, slot, property, slotRow.Id));
                    }
                }
            }

            return rows;
        }

        private void EnsureMaterialFoldoutDefaults()
        {
            RuntimeMaterialShaderSection section = _details?.MaterialsAndShaders;
            if (section?.Renderers == null)
                return;

            foreach (RuntimeRendererMaterialDescriptor renderer in section.Renderers)
            {
                if (_knownMaterialRenderers.Add(renderer.RendererId.Value))
                    _expandedMaterialRenderers.Add(renderer.RendererId.Value);
                foreach (RuntimeMaterialSlotDescriptor slot in renderer.MaterialSlots ?? Array.Empty<RuntimeMaterialSlotDescriptor>())
                {
                    var key = new MaterialSlotKey(renderer.RendererId.Value, slot.MaterialIndex);
                    if (_knownMaterialSlots.Add(key))
                        _expandedMaterialSlots.Add(key);
                }
            }
        }

        private bool EditingTargetExists()
        {
            if (IsEditingMemberValue)
            {
                return _details != null && _details.Components.Any(component => component.Id.Equals(_editingComponent.Id) && component.Members != null && component.Members.Any(member => string.Equals(member.Name, _editingMember.Name, StringComparison.Ordinal)));
            }

            if (!IsEditingShaderValue)
                return true;

            return FindShaderProperty(_editingRendererId, _editingMaterialIndex, _editingShaderProperty.Property.PropertyId) != null;
        }

        private RuntimeShaderPropertyView FindShaderProperty(RuntimeObjectId rendererId, int materialIndex, int propertyId)
        {
            RuntimeMaterialShaderSection section = _details?.MaterialsAndShaders;
            if (section?.Renderers == null)
                return null;

            RuntimeRendererMaterialDescriptor renderer = section.Renderers.FirstOrDefault(item => item.RendererId.Equals(rendererId));
            RuntimeMaterialSlotDescriptor slot = renderer?.MaterialSlots?.FirstOrDefault(item => item.MaterialIndex == materialIndex);
            return slot?.Properties?.FirstOrDefault(item => item.Property.PropertyId == propertyId);
        }

        private void ClearEditingTargets()
        {
            _editingComponent = null;
            _editingMember = null;
            _editingRendererId = default;
            _editingMaterialIndex = -1;
            _editingShaderProperty = null;
        }

        private static void SetContains<T>(HashSet<T> set, T value, bool contains)
        {
            if (contains)
                set.Add(value);
            else
                set.Remove(value);
        }

        private enum InspectorRowKind
        {
            Active,
            Component,
            ComponentMember,
            MaterialSection,
            MaterialRenderer,
            MaterialSlot,
            ShaderProperty
        }

        private sealed class InspectorRow
        {
            internal InspectorRowKind Kind;
            internal string Id;
            internal string ParentId;
            internal RuntimeComponentDescriptor Component;
            internal RuntimeMemberDescriptor Member;
            internal RuntimeMaterialShaderSection Section;
            internal RuntimeRendererMaterialDescriptor Renderer;
            internal RuntimeMaterialSlotDescriptor Slot;
            internal RuntimeShaderPropertyView ShaderProperty;

            internal static InspectorRow Active() =>
                new()
                {
                    Kind = InspectorRowKind.Active,
                    Id = "$active"
                };

            internal static InspectorRow ForComponent(RuntimeComponentDescriptor component) =>
                new()
                {
                    Kind = InspectorRowKind.Component,
                    Id = $"component:{component.Id.Value}",
                    Component = component
                };

            internal static InspectorRow ForMember(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member, string parentId) =>
                new()
                {
                    Kind = InspectorRowKind.ComponentMember,
                    Id = $"component:{component.Id.Value}:member:{member.Name}",
                    ParentId = parentId,
                    Component = component,
                    Member = member
                };

            internal static InspectorRow ForMaterialSection(RuntimeMaterialShaderSection section) =>
                new()
                {
                    Kind = InspectorRowKind.MaterialSection,
                    Id = "shader:section",
                    Section = section
                };

            internal static InspectorRow ForMaterialRenderer(RuntimeRendererMaterialDescriptor renderer, string parentId) =>
                new()
                {
                    Kind = InspectorRowKind.MaterialRenderer,
                    Id = $"shader:renderer:{renderer.RendererId.Value}",
                    ParentId = parentId,
                    Renderer = renderer
                };

            internal static InspectorRow ForMaterialSlot(RuntimeRendererMaterialDescriptor renderer, RuntimeMaterialSlotDescriptor slot, string parentId) =>
                new()
                {
                    Kind = InspectorRowKind.MaterialSlot,
                    Id = $"shader:slot:{renderer.RendererId.Value}:{slot.MaterialIndex}",
                    ParentId = parentId,
                    Renderer = renderer,
                    Slot = slot
                };

            internal static InspectorRow ForShaderProperty(RuntimeRendererMaterialDescriptor renderer, RuntimeMaterialSlotDescriptor slot, RuntimeShaderPropertyView property, string parentId) =>
                new()
                {
                    Kind = InspectorRowKind.ShaderProperty,
                    Id = $"shader:property:{renderer.RendererId.Value}:{slot.MaterialIndex}:" + property.Property.PropertyId,
                    ParentId = parentId,
                    Renderer = renderer,
                    Slot = slot,
                    ShaderProperty = property
                };
        }

        private readonly struct MaterialSlotKey : IEquatable<MaterialSlotKey>
        {
            internal MaterialSlotKey(long rendererId, int materialIndex)
            {
                RendererId = rendererId;
                MaterialIndex = materialIndex;
            }

            private long RendererId { get; }
            private int MaterialIndex { get; }
            public bool Equals(MaterialSlotKey other) => RendererId == other.RendererId && MaterialIndex == other.MaterialIndex;
            public override bool Equals(object obj) => obj is MaterialSlotKey other && Equals(other);
            public override int GetHashCode() => unchecked((int)(RendererId * 397) ^ MaterialIndex);
        }
    }
}

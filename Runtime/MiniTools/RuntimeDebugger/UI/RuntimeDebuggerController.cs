using System;
using System.Collections.Generic;
using SAS.Utilities.RuntimeDebugger.Core;
using SAS.Utilities.RuntimeDebugger.Input;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    internal sealed class RuntimeDebuggerController : IDisposable
    {
        private readonly RuntimeDebuggerSettings _settings;
        private readonly IRuntimeDebugger _service;
        private readonly IDisposable _ownedService;
        private readonly InputSystemRuntimeDebuggerInput _input;
        private readonly RuntimeDebuggerHierarchyController _hierarchy;
        private readonly RuntimeDebuggerInspectorController _inspector;
        private RuntimeDebuggerPanel _focusedPanel = RuntimeDebuggerPanel.Hierarchy;
        private string _error = string.Empty;
        private float _nextRefresh;
        private float _savedTimeScale;
        private bool _open;
        private bool _focusSearchField;
        private bool _clearGuiFocus;

        internal RuntimeDebuggerController(RuntimeDebuggerSettings settings) : this(settings, new RuntimeDebuggerService(settings), true)
        {
        }

        internal RuntimeDebuggerController(RuntimeDebuggerSettings settings, IRuntimeDebugger service, bool ownsService)
        {
            _settings = settings;
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _ownedService = ownsService ? service as IDisposable : null;
            _input = new InputSystemRuntimeDebuggerInput(settings);
            _hierarchy = new RuntimeDebuggerHierarchyController(_service);
            _inspector = new RuntimeDebuggerInspectorController(_service, settings);
            Refresh();
        }

        internal bool IsOpen => _open;
        internal bool ConsumesGameplayInput => _open && _settings.ConsumeInput;
        internal RuntimeHierarchySnapshot Snapshot => _hierarchy.Snapshot;
        internal RuntimeObjectDetails Details => _inspector.Details;
        internal List<RuntimeHierarchyEntry> VisibleEntries => _hierarchy.VisibleEntries;
        internal HashSet<long> ExpandedHierarchy => _hierarchy.ExpandedEntries;
        internal HashSet<long> ExpandedComponents => _inspector.ExpandedComponents;
        internal bool MaterialsExpanded => _inspector.MaterialsExpanded;
        internal string ShaderPropertySearch => _inspector.ShaderPropertySearch;
        internal RuntimeDebuggerPanel FocusedPanel => _focusedPanel;
        internal bool IsSearchFocused => _focusedPanel == RuntimeDebuggerPanel.Search;
        internal bool IsHierarchyFocused => _focusedPanel == RuntimeDebuggerPanel.Hierarchy;
        internal bool IsInspectorFocused => _focusedPanel == RuntimeDebuggerPanel.Inspector;
        internal bool IsEditing => _inspector.IsEditing;
        internal string Search => _hierarchy.Search;
        internal string Error => _error;
        internal int HierarchyCursor => _hierarchy.Cursor;
        internal int InspectorCursor => _inspector.Cursor;

        internal string EditValue
        {
            get => _inspector.EditValue;
            set => _inspector.EditValue = value;
        }

        internal bool FocusSearchField
        {
            get => _focusSearchField;
            set => _focusSearchField = value;
        }

        internal bool FocusEditField
        {
            get => _inspector.FocusEditField;
            set => _inspector.FocusEditField = value;
        }

        internal bool ClearGuiFocus
        {
            get => _clearGuiFocus;
            set => _clearGuiFocus = value;
        }

        internal bool RevealInspectorCursor
        {
            get => _inspector.RevealCursor;
            set => _inspector.RevealCursor = value;
        }

        internal bool RevealHierarchyCursor
        {
            get => _hierarchy.RevealCursor;
            set => _hierarchy.RevealCursor = value;
        }

        internal void Tick()
        {
            _input.Update();
            if (!_open)
                return;

            if (_settings.AutomaticRefresh && Time.unscaledTime >= _nextRefresh)
                Refresh();
            if (_input.Refresh)
                Refresh();

            if (_input.ShiftTab)
            {
                CycleFocusedPanel(-1);
                return;
            }

            if (_input.Tab)
            {
                CycleFocusedPanel(1);
                return;
            }

            if (_inspector.IsEditing)
            {
                HandleEditingInput();
                return;
            }

            if (_input.Cancel)
            {
                if (IsSearchFocused && _hierarchy.Search.Length > 0)
                {
                    _hierarchy.SetSearch(string.Empty);
                    _focusSearchField = true;
                }
                else
                {
                    SetOpen(false);
                }

                return;
            }

            if (IsSearchFocused)
            {
                if (_input.ClearSearch && _hierarchy.Search.Length > 0)
                    _hierarchy.SetSearch(string.Empty);
                return;
            }

            if (IsInspectorFocused)
                HandleInspectorInput();
            else
                HandleHierarchyInput();
        }

        internal void SetOpen(bool value)
        {
            if (_open == value)
                return;

            _open = value;
            if (value)
            {
                SetFocusedPanel(RuntimeDebuggerPanel.Hierarchy);
                _input.ResetNavigationUntilNeutral();
                Refresh();
                if (_settings.PauseWhenOpen)
                {
                    _savedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }

                return;
            }

            if (_inspector.IsEditing)
                CancelEdit();
            _focusedPanel = RuntimeDebuggerPanel.Hierarchy;
            _focusSearchField = false;
            _inspector.RevealCursor = false;
            _clearGuiFocus = true;
            if (_settings.PauseWhenOpen)
                Time.timeScale = _savedTimeScale;
        }

        internal void SetFocusedPanel(RuntimeDebuggerPanel panel)
        {
            if (_focusedPanel == panel)
            {
                if (panel == RuntimeDebuggerPanel.Search)
                    _focusSearchField = true;
                else
                    _clearGuiFocus = true;
                _input.ResetNavigationUntilNeutral();
                return;
            }

            if (_inspector.IsEditing)
                CancelEdit();

            _focusedPanel = panel;
            _focusSearchField = panel == RuntimeDebuggerPanel.Search;
            _inspector.RevealCursor = panel == RuntimeDebuggerPanel.Inspector;
            _clearGuiFocus = true;
            _input.ResetNavigationUntilNeutral();
        }

        internal void SetSearch(string value) => _hierarchy.SetSearch(value);

        internal void ActivateHierarchyRow(int index)
        {
            SetFocusedPanel(RuntimeDebuggerPanel.Hierarchy);
            RuntimeObjectId selectedId = _hierarchy.ActivateRow(index);
            if (selectedId.IsValid)
                _inspector.Select(selectedId);
        }

        internal void ActivateInspectedObjectRow(int rowIndex)
        {
            if (_inspector.IsEditing)
                CancelEdit();
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            ApplyCommand(_inspector.ToggleInspectedObjectActive(rowIndex), true);
        }

        internal void ToggleComponentFoldout(RuntimeComponentDescriptor component, int rowIndex)
        {
            if (_inspector.IsEditing)
                CancelEdit();
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _inspector.ToggleFoldout(component, rowIndex);
        }

        internal void ToggleComponentFromView(RuntimeComponentDescriptor component, int rowIndex)
        {
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            ApplyCommand(_inspector.ToggleComponentEnabled(component, rowIndex), true);
        }

        internal bool HasHierarchyChildren(RuntimeHierarchyEntry entry) => _hierarchy.HasChildren(entry);

        internal static bool HasInspectorMembers(RuntimeComponentDescriptor component) =>
            RuntimeDebuggerInspectorController.HasMembers(component);

        internal bool IsEditingMember(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member) =>
            _inspector.IsEditingMember(component, member);

        internal bool IsEditingShaderProperty(RuntimeObjectId rendererId, int materialIndex, int propertyId) =>
            _inspector.IsEditingShaderProperty(rendererId, materialIndex, propertyId);

        internal bool IsMaterialRendererExpanded(RuntimeObjectId rendererId) =>
            _inspector.IsMaterialRendererExpanded(rendererId);

        internal bool IsMaterialSlotExpanded(RuntimeObjectId rendererId, int materialIndex) =>
            _inspector.IsMaterialSlotExpanded(rendererId, materialIndex);

        internal RuntimeMaterialEditScope GetMaterialScope(RuntimeObjectId rendererId, int materialIndex) =>
            _inspector.GetMaterialScope(rendererId, materialIndex);

        internal bool IsMaterialScopeAllowed(RuntimeMaterialEditScope scope) =>
            _inspector.ScopeAllowed(scope);

        internal bool MatchesShaderProperty(RuntimeShaderPropertyDescriptor property) =>
            _inspector.MatchesShaderProperty(property);

        internal void SetShaderPropertySearch(string value) => _inspector.SetShaderPropertySearch(value);

        internal void BeginEditFromView(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member,
            int rowIndex)
        {
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _error = string.Empty;
            _inspector.BeginEdit(component, member, rowIndex);
        }

        internal void ToggleMaterialsFoldout(int rowIndex)
        {
            if (_inspector.IsEditing)
                CancelEdit();
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _inspector.ToggleMaterialsFoldout(rowIndex);
        }

        internal void ToggleMaterialRendererFoldout(RuntimeObjectId rendererId, int rowIndex)
        {
            if (_inspector.IsEditing)
                CancelEdit();
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _inspector.ToggleMaterialRendererFoldout(rendererId, rowIndex);
        }

        internal void ToggleMaterialSlotFoldout(RuntimeObjectId rendererId, int materialIndex, int rowIndex)
        {
            if (_inspector.IsEditing)
                CancelEdit();
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _inspector.ToggleMaterialSlotFoldout(rendererId, materialIndex, rowIndex);
        }

        internal void SetMaterialScope(RuntimeObjectId rendererId, int materialIndex,
            RuntimeMaterialEditScope scope)
        {
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _inspector.SetMaterialScope(rendererId, materialIndex, scope);
        }

        internal void BeginShaderEditFromView(RuntimeObjectId rendererId, int materialIndex,
            RuntimeShaderPropertyView property, int rowIndex)
        {
            SetFocusedPanel(RuntimeDebuggerPanel.Inspector);
            _error = string.Empty;
            _inspector.BeginShaderEdit(rendererId, materialIndex, property, rowIndex);
        }

        internal void CancelEdit()
        {
            _inspector.CancelEdit();
            _clearGuiFocus = true;
        }

        internal void CommitEdit()
        {
            RuntimeCommandResult result = _inspector.CommitEdit();
            _error = result.Message;
            if (result.Success)
            {
                _clearGuiFocus = true;
                Refresh();
            }
        }

        internal bool ApplyShaderProperty(RuntimeObjectId rendererId, int materialIndex, int propertyId, RuntimeMaterialEditScope scope, string value)
        {
            RuntimeCommandResult result = _service.Execute(new SetRuntimeShaderPropertyCommand
            {
                RendererId = rendererId,
                MaterialIndex = materialIndex,
                PropertyId = propertyId,
                Scope = scope,
                Value = value
            });
            ApplyCommand(result, result.Success);
            return result.Success;
        }

        internal void RestoreShaderProperty(RuntimeObjectId rendererId, int materialIndex, int propertyId, RuntimeMaterialEditScope scope)
        {
            ApplyCommand(_service.Execute(new RestoreRuntimeShaderPropertyCommand
            {
                RendererId = rendererId,
                MaterialIndex = materialIndex,
                PropertyId = propertyId,
                Scope = scope
            }), true);
        }

        internal void RestoreShaderMaterial(RuntimeObjectId rendererId, int materialIndex, RuntimeMaterialEditScope scope)
        {
            ApplyCommand(_service.Execute(new RestoreRuntimeMaterialCommand
            {
                RendererId = rendererId,
                MaterialIndex = materialIndex,
                Scope = scope
            }), true);
        }

        internal void SetInputEnabled(bool value) => _input.SetEnabled(value);

        public void Dispose()
        {
            if (_open)
                SetOpen(false);
            _input.Dispose();
            _ownedService?.Dispose();
        }

        private void HandleEditingInput()
        {
            if (_input.Cancel)
                CancelEdit();
            else if (_input.Confirm)
                CommitEdit();
        }

        private void HandleHierarchyInput()
        {
            if (_hierarchy.VisibleEntries.Count == 0)
                return;

            if (TryGetNavigationCommand(out RuntimeDebuggerNavigationCommand command))
                _hierarchy.Navigate(command);

            if (_input.Confirm)
            {
                RuntimeObjectId selectedId = _hierarchy.CurrentGameObjectId();
                if (selectedId.IsValid)
                    _inspector.Select(selectedId);
            }

            if (_input.Space && _hierarchy.CurrentGameObjectId().IsValid)
                ApplyCommand(_hierarchy.ToggleCurrentActive(), true);
        }

        private void HandleInspectorInput()
        {
            if (TryGetNavigationCommand(out RuntimeDebuggerNavigationCommand command))
                _inspector.Navigate(command);

            RuntimeCommandResult result = _inspector.ActivateCurrent(_input.Confirm, _input.Space,
                out bool requiresRefresh);
            if (result != null)
                ApplyCommand(result, requiresRefresh);
        }

        private bool TryGetNavigationCommand(out RuntimeDebuggerNavigationCommand command)
        {
            if (_input.Home)
                command = RuntimeDebuggerNavigationCommand.Home;
            else if (_input.End)
                command = RuntimeDebuggerNavigationCommand.End;
            else if (_input.PageUp)
                command = RuntimeDebuggerNavigationCommand.PageUp;
            else if (_input.PageDown)
                command = RuntimeDebuggerNavigationCommand.PageDown;
            else if (_input.Up)
                command = RuntimeDebuggerNavigationCommand.Up;
            else if (_input.Down)
                command = RuntimeDebuggerNavigationCommand.Down;
            else if (_input.Right)
                command = RuntimeDebuggerNavigationCommand.Right;
            else if (_input.Left)
                command = RuntimeDebuggerNavigationCommand.Left;
            else
            {
                command = default;
                return false;
            }

            return true;
        }

        private void CycleFocusedPanel(int direction)
        {
            const int panelCount = 3;
            int panelIndex = ((int)_focusedPanel + direction + panelCount) % panelCount;
            SetFocusedPanel((RuntimeDebuggerPanel)panelIndex);
        }

        private void ApplyCommand(RuntimeCommandResult result, bool refresh)
        {
            if (result == null)
                return;
            _error = result.Message;
            if (refresh)
                Refresh();
        }

        private void Refresh()
        {
            _hierarchy.Refresh();
            string inspectorMessage = _inspector.Refresh();
            if (!string.IsNullOrEmpty(inspectorMessage))
                _error = inspectorMessage;
            _nextRefresh = Time.unscaledTime + _settings.HierarchyRefreshInterval;
        }
    }
}

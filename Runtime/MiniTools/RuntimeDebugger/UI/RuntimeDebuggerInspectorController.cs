using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SAS.Utilities.RuntimeDebugger.Core;
using SAS.Utilities.RuntimeDebugger.Input;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    internal sealed class RuntimeDebuggerInspectorController
    {
        private const string ActiveRowName = "$active";
        private const string ComponentRowName = "$component";
        private readonly RuntimeDebuggerService _service;
        private readonly RuntimeDebuggerSettings _settings;
        private readonly HashSet<long> _expandedComponents = new();
        private RuntimeObjectDetails _details;
        private RuntimeMemberDescriptor _editingMember;
        private RuntimeComponentDescriptor _editingComponent;
        private string _editValue = string.Empty;
        private int _cursor;

        internal RuntimeDebuggerInspectorController(RuntimeDebuggerService service,
            RuntimeDebuggerSettings settings)
        {
            _service = service;
            _settings = settings;
        }

        internal RuntimeObjectDetails Details => _details;
        internal HashSet<long> ExpandedComponents => _expandedComponents;
        internal int Cursor => _cursor;
        internal bool IsEditing => _editingComponent != null && _editingMember != null;
        internal bool FocusEditField { get; set; }
        internal bool RevealCursor { get; set; }

        internal string EditValue
        {
            get => _editValue;
            set => _editValue = value;
        }

        internal string Refresh()
        {
            RuntimeObjectId selectedComponentId = default;
            string selectedRowName = null;
            List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> previousRows = GetRows();
            if (_cursor >= 0 && _cursor < previousRows.Count)
            {
                var selectedRow = previousRows[_cursor];
                selectedComponentId = selectedRow.component?.Id ?? default;
                selectedRowName = selectedRow.member.Name;
            }

            if (_details != null)
                _details = _service.InspectObject(_details.Id);
            if (IsEditing && !EditingTargetExists())
            {
                CancelEdit();
                RestoreCursor(selectedComponentId, selectedRowName);
                return "The edited value is no longer available.";
            }

            RestoreCursor(selectedComponentId, selectedRowName);
            return null;
        }

        internal void Select(RuntimeObjectId id)
        {
            if (IsEditing)
                CancelEdit();
            _details = _service.InspectObject(id);
            _cursor = 0;
            RevealCursor = true;
            if (_details == null)
                return;
            foreach (RuntimeComponentDescriptor component in _details.Components)
            {
                if (HasMembers(component))
                    _expandedComponents.Add(component.Id.Value);
            }
        }

        internal void Navigate(RuntimeDebuggerNavigationCommand command)
        {
            List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> rows = GetRows();
            if (rows.Count == 0)
                return;

            int previousCursor = _cursor;
            _cursor = Mathf.Clamp(_cursor, 0, rows.Count - 1);
            switch (command)
            {
                case RuntimeDebuggerNavigationCommand.Up:
                    _cursor--;
                    break;
                case RuntimeDebuggerNavigationCommand.Down:
                    _cursor++;
                    break;
                case RuntimeDebuggerNavigationCommand.Home:
                    _cursor = 0;
                    break;
                case RuntimeDebuggerNavigationCommand.End:
                    _cursor = rows.Count - 1;
                    break;
                case RuntimeDebuggerNavigationCommand.PageUp:
                    _cursor -= 8;
                    break;
                case RuntimeDebuggerNavigationCommand.PageDown:
                    _cursor += 8;
                    break;
                case RuntimeDebuggerNavigationCommand.Left:
                case RuntimeDebuggerNavigationCommand.Right:
                    NavigateComponent(rows, command);
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
            List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> rows = GetRows();
            if (rows.Count == 0)
                return null;

            _cursor = Mathf.Clamp(_cursor, 0, rows.Count - 1);
            var selected = rows[_cursor];
            if (selected.member.Name == ActiveRowName)
            {
                if (!confirm && !space)
                    return null;
                requiresRefresh = true;
                return ToggleInspectedObjectActive();
            }

            if (selected.member.Name == ComponentRowName)
            {
                if (confirm || space && !selected.component.HasEnabledState)
                {
                    ToggleFoldout(selected.component);
                    return null;
                }

                if (!space)
                    return null;
                requiresRefresh = true;
                return ToggleComponentEnabled(selected.component);
            }

            if (confirm && !selected.member.ReadOnly)
                BeginEdit(selected.component, selected.member);
            return null;
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

        internal RuntimeCommandResult ToggleInspectedObjectActive(int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            if (_details == null)
                return RuntimeCommandResult.Fail("No object is being inspected.");
            return _service.Execute(new SetGameObjectActiveCommand
                { ObjectId = _details.Id, Active = !_details.Active });
        }

        internal RuntimeCommandResult ToggleComponentEnabled(RuntimeComponentDescriptor component, int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            if (IsEditing)
                CancelEdit();
            return _service.Execute(new SetComponentEnabledCommand
                { ComponentId = component.Id, Enabled = !component.Enabled });
        }

        internal void BeginEdit(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member, int rowIndex = -1)
        {
            if (rowIndex >= 0)
                _cursor = rowIndex;
            _editingComponent = component;
            _editingMember = member;
            _editValue = member.Value;
            FocusEditField = true;
        }

        internal void CancelEdit()
        {
            _editingComponent = null;
            _editingMember = null;
            FocusEditField = false;
        }

        internal RuntimeCommandResult CommitEdit()
        {
            if (_editingComponent == null || _editingMember == null)
            {
                CancelEdit();
                return RuntimeCommandResult.Fail("The edited value is no longer available.");
            }

            RuntimeCommandResult result = _service.Execute(new SetMemberValueCommand
                { ComponentId = _editingComponent.Id, MemberName = _editingMember.Name, Value = _editValue });
            if (!result.Success)
            {
                FocusEditField = true;
                return result;
            }

            CancelEdit();
            return result;
        }

        internal void AdjustEdit(float direction, InputSystemRuntimeDebuggerInput input)
        {
            if (!double.TryParse(_editValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return;
            float step = input.LargeStepModifier ? _settings.LargeNumericStep :
                input.SmallStepModifier ? _settings.SmallNumericStep : _settings.NormalNumericStep;
            _editValue = (value + direction * step).ToString(CultureInfo.InvariantCulture);
        }

        internal bool IsEditingMember(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member) =>
            IsEditing && component.Id.Equals(_editingComponent.Id) &&
            string.Equals(member.Name, _editingMember.Name, StringComparison.Ordinal);

        internal static bool HasMembers(RuntimeComponentDescriptor component) =>
            component.Members != null && component.Members.Count > 0;

        private void NavigateComponent(List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> rows, RuntimeDebuggerNavigationCommand command)
        {
            var selected = rows[_cursor];
            if (selected.component == null)
                return;
            int componentRow = rows.FindIndex(row => row.component != null &&
                                                     row.component.Id.Equals(selected.component.Id) &&
                                                     row.member.Name == ComponentRowName);
            if (componentRow < 0)
                return;

            if (!HasMembers(selected.component))
            {
                _expandedComponents.Remove(selected.component.Id.Value);
                _cursor = componentRow;
                return;
            }

            bool expanded = _expandedComponents.Contains(selected.component.Id.Value);
            if (command == RuntimeDebuggerNavigationCommand.Left)
            {
                if (selected.member.Name != ComponentRowName)
                {
                    _cursor = componentRow;
                    RevealCursor = true;
                    return;
                }

                _expandedComponents.Remove(selected.component.Id.Value);
                RevealCursor = true;
                return;
            }

            if (!expanded)
            {
                _expandedComponents.Add(selected.component.Id.Value);
                RevealCursor = true;
                return;
            }

            if (selected.member.Name == ComponentRowName && componentRow + 1 < rows.Count &&
                rows[componentRow + 1].component != null &&
                rows[componentRow + 1].component.Id.Equals(selected.component.Id))
                _cursor = componentRow + 1;
        }

        private void RestoreCursor(RuntimeObjectId componentId, string rowName)
        {
            List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> rows = GetRows();
            int previousCursor = _cursor;
            int restoredCursor = -1;
            if (!string.IsNullOrEmpty(rowName))
                restoredCursor = rows.FindIndex(row =>
                    string.Equals(row.member.Name, rowName, StringComparison.Ordinal) &&
                    (componentId.IsValid
                        ? row.component != null && row.component.Id.Equals(componentId)
                        : row.component == null));

            _cursor = restoredCursor >= 0
                ? restoredCursor
                : Mathf.Clamp(previousCursor, 0, Mathf.Max(0, rows.Count - 1));
            if (_cursor != previousCursor || !string.IsNullOrEmpty(rowName) && restoredCursor < 0)
                RevealCursor = true;
        }

        private List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> GetRows()
        {
            var rows = new List<(RuntimeComponentDescriptor, RuntimeMemberDescriptor)>();
            if (_details == null)
                return rows;
            rows.Add((null, new RuntimeMemberDescriptor
            {
                Name = ActiveRowName, DisplayName = "Active", Value = _details.Active.ToString(), ReadOnly = true
            }));
            foreach (RuntimeComponentDescriptor component in _details.Components)
            {
                if (component.Missing)
                    continue;
                rows.Add((component, new RuntimeMemberDescriptor
                    { Name = ComponentRowName, DisplayName = component.TypeName, ReadOnly = true }));
                if (_expandedComponents.Contains(component.Id.Value) && component.Members != null)
                    foreach (RuntimeMemberDescriptor member in component.Members)
                        rows.Add((component, member));
            }

            return rows;
        }

        private bool EditingTargetExists() =>
            !IsEditing || _details != null && _details.Components.Any(component =>
                component.Id.Equals(_editingComponent.Id) && component.Members != null &&
                component.Members.Any(member =>
                    string.Equals(member.Name, _editingMember.Name, StringComparison.Ordinal)));
    }
}

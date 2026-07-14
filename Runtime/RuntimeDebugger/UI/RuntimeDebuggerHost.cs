using System;
using System.Collections.Generic;
using System.Linq;
using SAS.Utilities.RuntimeDebugger.Core;
using SAS.Utilities.RuntimeDebugger.Input;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.Utilities.RuntimeDebugger
{
    public sealed class RuntimeDebuggerHost : MonoBehaviour
    {
        private const int ResizeControlHint = 0x52D38;
        private static readonly ProfilerMarker UiMarker = new("RuntimeDebugger.UI.Rebuild");
        public static RuntimeDebuggerHost Instance { get; private set; }
        public bool IsOpen => _open;
        public bool IsDebuggerEnabled => enabled && _service != null;
        public bool ConsumesGameplayInput => _open && _settings != null && _settings.ConsumeInput;
        private RuntimeDebuggerSettings _settings;
        private RuntimeDebuggerService _service;
        private InputSystemRuntimeDebuggerInput _input;
        private RuntimeHierarchySnapshot _snapshot;
        private RuntimeObjectDetails _details;
        private readonly HashSet<long> _expanded = new();
        private readonly HashSet<long> _expandedComponents = new();
        private readonly HashSet<long> _knownScenes = new();
        private readonly List<RuntimeHierarchyEntry> _visible = new();
        private Vector2 _hierarchyScroll, _inspectorScroll;
        private Rect _window = new(80, 60, 1100, 700);
        private Vector2 _pendingWindowSize;
        private int _cursor, _inspectorCursor;
        private bool _open, _inspectorFocused, _searchFocused, _editing;
        private string _search = "", _editValue = "", _error = "";
        private float _nextRefresh, _savedTimeScale;
        private RuntimeMemberDescriptor _editingMember;
        private RuntimeComponentDescriptor _editingComponent;

        internal void Initialize(RuntimeDebuggerSettings settings) => _settings = settings;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            _settings ??= RuntimeDebuggerSettings.LoadOrCreateDefaults();
            StartSubsystem();
        }

        private void StartSubsystem()
        {
            if (_service != null)
                return;

            _service = new RuntimeDebuggerService(_settings);
            _input = new InputSystemRuntimeDebuggerInput(_settings);
            Refresh();
        }

        private void OnDestroy() { _service?.Dispose(); if (Instance == this) Instance = null; }

        public void SetDebuggerEnabled(bool value)
        {
            if (value == IsDebuggerEnabled)
                return;

            if (!value)
            {
                SetOpen(false);
                _service?.Dispose();
                _service = null;
                _input = null;
                _snapshot = null;
                _details = null;
                _visible.Clear();
                enabled = false;
                return;
            }

            enabled = true;
            StartSubsystem();
        }

        public void SetOverlayVisible(bool visible)
        {
            if (visible && !IsDebuggerEnabled)
                SetDebuggerEnabled(true);

            SetOpen(visible && IsDebuggerEnabled);
        }

        public static RuntimeDebuggerHost GetOrCreateEnabledHost()
        {
            if (Instance != null)
            {
                Instance.SetDebuggerEnabled(true);
                return Instance;
            }

            RuntimeDebuggerSettings settings = RuntimeDebuggerSettings.LoadOrCreateDefaults();
            if (!settings.EnableDebugger)
                return null;

            var hostObject = new GameObject("[Runtime Debugger]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(hostObject);
            var host = hostObject.AddComponent<RuntimeDebuggerHost>();
            host.Initialize(settings);
            return host;
        }

        private void Update()
        {
            _input.Update();
            if (!_open) return;
            if (_settings.AutomaticRefresh && Time.unscaledTime >= _nextRefresh) Refresh();
            if (_input.Refresh) Refresh();
            if (_editing) { if (_input.Cancel) CancelEdit(); else if (_input.Confirm) CommitEdit(); else if (_input.Left) AdjustEdit(-1f); else if (_input.Right) AdjustEdit(1f); return; }
            if (_searchFocused) { if (_input.Cancel || _input.Confirm) _searchFocused = false; return; }
            if (_input.Search) { _searchFocused = true; return; }
            if (_input.Cancel) { if (_search.Length > 0) { _search = ""; RebuildVisible(); } else if (_inspectorFocused) _inspectorFocused = false; else SetOpen(false); return; }
            if (_input.ShiftTab) _inspectorFocused = false; else if (_input.Tab) _inspectorFocused = !_inspectorFocused;
            if (_inspectorFocused) HandleInspectorInput(); else HandleHierarchyInput();
        }

        private void HandleHierarchyInput()
        {
            if (_visible.Count == 0) return;
            if (_input.Home) _cursor = 0; else if (_input.End) _cursor = _visible.Count - 1; else if (_input.PageUp) _cursor -= 12; else if (_input.PageDown) _cursor += 12; else if (_input.Up) _cursor--; else if (_input.Down) _cursor++;
            _cursor = Mathf.Clamp(_cursor, 0, _visible.Count - 1);
            _hierarchyScroll.y = Mathf.Max(0f, _cursor * 20f - Mathf.Max(100f, _window.height - 150f) * 0.5f);
            RuntimeHierarchyEntry entry = _visible[_cursor];
            if (_input.Right) { if (!_expanded.Contains(entry.Id.Value)) { _expanded.Add(entry.Id.Value); RebuildVisible(); } else if (_cursor + 1 < _visible.Count && _visible[_cursor + 1].ParentId.Equals(entry.Id)) _cursor++; }
            if (_input.Left) { if (_expanded.Remove(entry.Id.Value)) RebuildVisible(); else { int parent = _visible.FindIndex(item => item.Id.Equals(entry.ParentId)); if (parent >= 0) _cursor = parent; } }
            if (_input.Confirm && entry.Kind == RuntimeHierarchyKind.GameObject) Select(entry.Id);
            if (_input.Space && entry.Kind == RuntimeHierarchyKind.GameObject) { RuntimeCommandResult result = _service.Execute(new SetGameObjectActiveCommand { ObjectId = entry.Id, Active = !entry.ActiveSelf }); _error = result.Message; Refresh(); }
        }

        private void HandleInspectorInput()
        {
            List<(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member)> fields = GetInspectorFields();
            if (fields.Count == 0) return;
            if (_input.Up) _inspectorCursor--; else if (_input.Down) _inspectorCursor++;
            _inspectorCursor = Mathf.Clamp(_inspectorCursor, 0, fields.Count - 1);
            var selected = fields[_inspectorCursor];
            if ((_input.Confirm || _input.Space) && !selected.member.ReadOnly) BeginEdit(selected.component, selected.member);
            if (_input.Space && selected.member.ReadOnly && selected.member.Name == "$enabled") ToggleComponent(selected.component);
        }

        private void SetOpen(bool value)
        {
            _open = value;
            if (value) { Refresh(); if (_settings.PauseWhenOpen) { _savedTimeScale = Time.timeScale; Time.timeScale = 0f; } }
            else if (_settings.PauseWhenOpen) Time.timeScale = _savedTimeScale;
        }

        private void Refresh()
        {
            _service.RefreshHierarchy(); _snapshot = _service.GetHierarchySnapshot(); _nextRefresh = Time.unscaledTime + _settings.HierarchyRefreshInterval;
            foreach (RuntimeHierarchyEntry scene in _snapshot.Entries.Where(item => item.Kind == RuntimeHierarchyKind.Scene)) if (_knownScenes.Add(scene.Id.Value)) _expanded.Add(scene.Id.Value);
            if (_details != null) _details = _service.InspectObject(_details.Id);
            RebuildVisible();
        }

        private void RebuildVisible()
        {
            using (UiMarker.Auto())
            {
                _visible.Clear(); if (_snapshot == null) return;
                var byId = _snapshot.Entries.ToDictionary(item => item.Id.Value);
                HashSet<long> matches = null;
                if (!string.IsNullOrWhiteSpace(_search))
                {
                    matches = new HashSet<long>();
                    foreach (RuntimeHierarchyEntry item in _snapshot.Entries)
                        if ((item.Name?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || (item.ComponentTypeNames?.Any(type => type.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) ?? false))
                            for (RuntimeHierarchyEntry current = item; current != null && matches.Add(current.Id.Value) && byId.TryGetValue(current.ParentId.Value, out current);) { }
                }
                foreach (RuntimeHierarchyEntry item in _snapshot.Entries)
                {
                    if (matches != null && !matches.Contains(item.Id.Value)) continue;
                    if (matches == null && item.Kind == RuntimeHierarchyKind.GameObject && byId.TryGetValue(item.ParentId.Value, out RuntimeHierarchyEntry parent) && !_expanded.Contains(parent.Id.Value)) continue;
                    if (matches == null && HasCollapsedAncestor(item, byId)) continue;
                    _visible.Add(item);
                }
                _cursor = Mathf.Clamp(_cursor, 0, Mathf.Max(0, _visible.Count - 1));
            }
        }

        private bool HasCollapsedAncestor(RuntimeHierarchyEntry item, Dictionary<long, RuntimeHierarchyEntry> byId)
        {
            while (byId.TryGetValue(item.ParentId.Value, out RuntimeHierarchyEntry parent)) { if (!_expanded.Contains(parent.Id.Value)) return true; item = parent; }
            return false;
        }

        private void Select(RuntimeObjectId id) { _details = _service.InspectObject(id); _inspectorFocused = true; _inspectorCursor = 0; if (_details != null) foreach (RuntimeComponentDescriptor c in _details.Components) _expandedComponents.Add(c.Id.Value); }

        private List<(RuntimeComponentDescriptor, RuntimeMemberDescriptor)> GetInspectorFields()
        {
            var list = new List<(RuntimeComponentDescriptor, RuntimeMemberDescriptor)>(); if (_details == null) return list;
            foreach (RuntimeComponentDescriptor component in _details.Components)
            {
                if (component.HasEnabledState) list.Add((component, new RuntimeMemberDescriptor { Name = "$enabled", DisplayName = "Enabled", Value = component.Enabled.ToString(), ReadOnly = true }));
                if (_expandedComponents.Contains(component.Id.Value)) foreach (RuntimeMemberDescriptor member in component.Members) list.Add((component, member));
            }
            return list;
        }

        private void BeginEdit(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member) { _editing = true; _editingComponent = component; _editingMember = member; _editValue = member.Value; _error = ""; }
        private void CancelEdit() { _editing = false; _editingMember = null; }
        private void CommitEdit() { RuntimeCommandResult result = _service.Execute(new SetMemberValueCommand { ComponentId = _editingComponent.Id, MemberName = _editingMember.Name, Value = _editValue }); _error = result.Message; if (result.Success) { _editing = false; _details = _service.InspectObject(_details.Id); } }
        private void AdjustEdit(float direction)
        {
            if (!double.TryParse(_editValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double value)) return;
            float step = Keyboard.current != null && Keyboard.current.shiftKey.isPressed ? _settings.LargeNumericStep : Keyboard.current != null && Keyboard.current.ctrlKey.isPressed ? _settings.SmallNumericStep : _settings.NormalNumericStep;
            _editValue = (value + direction * step).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        private void ToggleComponent(RuntimeComponentDescriptor component) { RuntimeCommandResult result = _service.Execute(new SetComponentEnabledCommand { ComponentId = component.Id, Enabled = !component.Enabled }); _error = result.Message; _details = _service.InspectObject(_details.Id); }

        private void OnGUI()
        {
            if (!_open) return;
            float scale = Mathf.Clamp(_settings.UiScale, 0.75f, 2f);
            float logicalScreenWidth = Screen.width / scale;
            float logicalScreenHeight = Screen.height / scale;
            float minimumWidth = Mathf.Min(650f, logicalScreenWidth);
            float minimumHeight = Mathf.Min(400f, logicalScreenHeight);
            _window.width = Mathf.Clamp(_window.width, minimumWidth, logicalScreenWidth);
            _window.height = Mathf.Clamp(_window.height, minimumHeight, logicalScreenHeight);
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, logicalScreenWidth - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, logicalScreenHeight - _window.height));

            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Rect windowResult = GUI.Window(GetInstanceID(), _window, DrawWindow, "Runtime Debugger");
            if (_pendingWindowSize.x > 0f && _pendingWindowSize.y > 0f)
            {
                windowResult.size = _pendingWindowSize;
                _pendingWindowSize = Vector2.zero;
            }
            _window = windowResult;
            GUI.matrix = previousMatrix;
        }

        private void DrawWindow(int id)
        {
            GUI.backgroundColor = _settings.BackgroundColor;
            GUILayout.BeginHorizontal(); GUILayout.Label("Search:", GUILayout.Width(52)); GUI.SetNextControlName("Search"); string next = GUILayout.TextField(_search); if (next != _search) { _search = next; RebuildVisible(); } if (_searchFocused) GUI.FocusControl("Search"); if (GUILayout.Button("Clear", GUILayout.Width(50))) { _search = ""; RebuildVisible(); } GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); DrawHierarchy(); DrawInspector(); GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_error)) GUILayout.Label(_error);
            GUI.DragWindow(new Rect(0, 0, _window.width - 20, 24));
            Rect grip = new(_window.width - 18, _window.height - 18, 18, 18); GUI.Box(grip, "↘"); Event e = Event.current;
            int resizeControlId = GUIUtility.GetControlID(ResizeControlHint, FocusType.Passive, grip);
            EventType resizeEvent = e.GetTypeForControl(resizeControlId);
            if (resizeEvent == EventType.MouseDown && e.button == 0 && grip.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = resizeControlId;
                e.Use();
            }
            else if (resizeEvent == EventType.MouseDrag && GUIUtility.hotControl == resizeControlId)
            {
                float scale = Mathf.Clamp(_settings.UiScale, 0.75f, 2f);
                float maximumWidth = Screen.width / scale - _window.x;
                float maximumHeight = Screen.height / scale - _window.y;
                Vector2 nextSize = _window.size + e.delta;
                _pendingWindowSize = new Vector2(
                    Mathf.Clamp(nextSize.x, Mathf.Min(650f, maximumWidth), maximumWidth),
                    Mathf.Clamp(nextSize.y, Mathf.Min(400f, maximumHeight), maximumHeight));
                e.Use();
            }
            else if (resizeEvent == EventType.MouseUp && GUIUtility.hotControl == resizeControlId)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private void DrawHierarchy()
        {
            GUILayout.BeginVertical(GUILayout.Width(_window.width * _settings.HierarchyPanelWidth)); GUILayout.Label(_inspectorFocused ? "Hierarchy" : "> Hierarchy"); _hierarchyScroll = GUILayout.BeginScrollView(_hierarchyScroll);
            for (int i = 0; i < _visible.Count; i++)
            {
                RuntimeHierarchyEntry entry = _visible[i]; int depth = Depth(entry); string arrow = entry.Kind == RuntimeHierarchyKind.Scene || _snapshot.Entries.Any(item => item.ParentId.Equals(entry.Id)) ? (_expanded.Contains(entry.Id.Value) ? "▼ " : "▶ ") : "  "; string inactive = entry.Kind == RuntimeHierarchyKind.GameObject && !entry.ActiveSelf ? " (inactive)" : "";
                Color old = GUI.backgroundColor; if (!_inspectorFocused && i == _cursor) GUI.backgroundColor = _settings.FocusColor;
                if (GUILayout.Button(new string(' ', depth * 3) + arrow + entry.Name + inactive, GUI.skin.label)) { _cursor = i; if (entry.Kind == RuntimeHierarchyKind.GameObject) Select(entry.Id); else { if (!_expanded.Remove(entry.Id.Value)) _expanded.Add(entry.Id.Value); RebuildVisible(); } }
                GUI.backgroundColor = old;
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
        }

        private int Depth(RuntimeHierarchyEntry entry) { int depth = 0; RuntimeObjectId parent = entry.ParentId; while (parent.IsValid && _snapshot.Entries.FirstOrDefault(item => item.Id.Equals(parent)) is RuntimeHierarchyEntry found) { depth++; parent = found.ParentId; } return depth; }

        private void DrawInspector()
        {
            GUILayout.BeginVertical(); GUILayout.Label(_inspectorFocused ? "> Inspector" : "Inspector"); _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
            if (_details == null) GUILayout.Label("Select an object."); else
            {
                GUILayout.BeginHorizontal(); GUILayout.Label($"{_details.Name}   Active: {_details.Active}   Tag: {_details.Tag}   Layer: {_details.Layer}"); if (GUILayout.Button(_details.Active ? "Deactivate" : "Activate", GUILayout.Width(90))) { RuntimeCommandResult r = _service.Execute(new SetGameObjectActiveCommand { ObjectId = _details.Id, Active = !_details.Active }); _error = r.Message; Refresh(); } GUILayout.EndHorizontal(); int fieldIndex = 0;
                foreach (RuntimeComponentDescriptor component in _details.Components)
                {
                    if (component.Missing) { GUILayout.Label("Missing Script"); continue; }
                    GUILayout.BeginHorizontal(); if (GUILayout.Button(_expandedComponents.Contains(component.Id.Value) ? "▼" : "▶", GUILayout.Width(24))) { if (!_expandedComponents.Remove(component.Id.Value)) _expandedComponents.Add(component.Id.Value); } GUILayout.Label(component.TypeName); if (component.HasEnabledState) { Color enabledOld = GUI.backgroundColor; if (_inspectorFocused && fieldIndex == _inspectorCursor) GUI.backgroundColor = _settings.FocusColor; if (GUILayout.Button(component.Enabled ? "Enabled" : "Disabled", GUILayout.Width(75))) ToggleComponent(component); GUI.backgroundColor = enabledOld; fieldIndex++; } GUILayout.EndHorizontal();
                    if (!_expandedComponents.Contains(component.Id.Value)) continue;
                    foreach (RuntimeMemberDescriptor member in component.Members)
                    {
                        Color old = GUI.backgroundColor; if (_inspectorFocused && fieldIndex == _inspectorCursor) GUI.backgroundColor = _settings.FocusColor;
                        GUILayout.BeginHorizontal(); GUILayout.Space(28); GUILayout.Label(member.DisplayName, GUILayout.Width(180)); if (_editing && ReferenceEquals(member, _editingMember)) _editValue = GUILayout.TextField(_editValue); else GUILayout.Label(member.Error ?? member.Value); if (!member.ReadOnly && GUILayout.Button("Edit", GUILayout.Width(42))) BeginEdit(component, member); GUILayout.EndHorizontal(); GUI.backgroundColor = old; fieldIndex++;
                    }
                }
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
        }
    }
}

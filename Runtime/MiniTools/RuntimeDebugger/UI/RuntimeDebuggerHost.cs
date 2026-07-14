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
        private readonly List<Texture2D> _themeTextures = new();
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _footerStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _toolbarStyle;
        private GUIStyle _summaryStyle;
        private GUIStyle _searchFieldStyle;
        private GUIStyle _valueFieldStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _successButtonStyle;
        private GUIStyle _warningButtonStyle;
        private GUIStyle _iconButtonStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _selectedRowStyle;
        private GUIStyle _sceneRowStyle;
        private GUIStyle _inactiveRowStyle;
        private GUIStyle _componentStyle;
        private GUIStyle _messageStyle;
        private GUIStyle _resizeHandleStyle;

        private static readonly Color WindowColor = new(0.055f, 0.065f, 0.085f, 0.98f);
        private static readonly Color CardColor = new(0.09f, 0.11f, 0.145f, 0.98f);
        private static readonly Color TextColor = new(0.92f, 0.95f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.78f, 0.88f);
        private static readonly Color MutedColor = new(0.55f, 0.61f, 0.7f);
        private static readonly Color FocusColor = new(0.22f, 0.75f, 1f);
        private static readonly Color SuccessColor = new(0.45f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new(1f, 0.67f, 0.28f);

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

        private void OnDestroy()
        {
            _service?.Dispose();
            DisposeTheme();
            if (Instance == this) Instance = null;
        }

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
                DisposeTheme();
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
            EnsureTheme();
            Rect windowResult = GUI.Window(GetInstanceID(), _window, DrawWindow, GUIContent.none, _windowStyle);
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
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("RUNTIME DEBUGGER", _titleStyle);
            GUILayout.Label("LIVE SCENE INSPECTION", _mutedStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_snapshot == null ? "REFRESHING" : $"{_visible.Count} ITEMS", _badgeStyle, GUILayout.Height(22f));
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal(_toolbarStyle);
            GUILayout.Label("SEARCH", _sectionStyle, GUILayout.Width(58f));
            GUI.SetNextControlName("Search");
            string next = GUILayout.TextField(_search, _searchFieldStyle, GUILayout.Height(28f));
            if (next != _search) { _search = next; RebuildVisible(); }
            if (_searchFocused) GUI.FocusControl("Search");
            if (GUILayout.Button("CLEAR", _buttonStyle, GUILayout.Width(64f), GUILayout.Height(28f))) { _search = ""; RebuildVisible(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawHierarchy();
            GUILayout.Space(8f);
            DrawInspector();
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            if (!string.IsNullOrEmpty(_error))
                GUILayout.Label($"STATUS  {_error}", _messageStyle);
            else if (_editing)
                GUILayout.Label("Enter applies changes  /  Left and right adjust numeric values  /  Esc cancels", _footerStyle);
            else
                GUILayout.Label("Enter select  /  Space toggle  /  Tab switch panel  /  Esc close", _footerStyle);

            GUI.DragWindow(new Rect(0f, 0f, _window.width, 55f));
            Rect grip = new(_window.width - 18, _window.height - 18, 18, 18); GUI.Box(grip, "\u2198", _resizeHandleStyle); Event e = Event.current;
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
            GUILayout.BeginVertical(_panelStyle, GUILayout.Width(_window.width * _settings.HierarchyPanelWidth));
            DrawSectionHeader("HIERARCHY", _inspectorFocused ? "VIEW" : "FOCUS");
            GUILayout.Space(4f);
            _hierarchyScroll = GUILayout.BeginScrollView(_hierarchyScroll);
            for (int i = 0; i < _visible.Count; i++)
            {
                RuntimeHierarchyEntry entry = _visible[i]; int depth = Depth(entry); string arrow = entry.Kind == RuntimeHierarchyKind.Scene || _snapshot.Entries.Any(item => item.ParentId.Equals(entry.Id)) ? (_expanded.Contains(entry.Id.Value) ? "\u25BC " : "\u25B6 ") : "  "; string inactive = entry.Kind == RuntimeHierarchyKind.GameObject && !entry.ActiveSelf ? " (inactive)" : "";
                GUIStyle rowStyle = !_inspectorFocused && i == _cursor ? _selectedRowStyle : entry.Kind == RuntimeHierarchyKind.Scene ? _sceneRowStyle : !entry.ActiveSelf ? _inactiveRowStyle : _rowStyle;
                if (GUILayout.Button(new string(' ', depth * 3) + arrow + entry.Name + inactive, rowStyle, GUILayout.Height(20f))) { _cursor = i; if (entry.Kind == RuntimeHierarchyKind.GameObject) Select(entry.Id); else { if (!_expanded.Remove(entry.Id.Value)) _expanded.Add(entry.Id.Value); RebuildVisible(); } }
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
        }

        private int Depth(RuntimeHierarchyEntry entry) { int depth = 0; RuntimeObjectId parent = entry.ParentId; while (parent.IsValid && _snapshot.Entries.FirstOrDefault(item => item.Id.Equals(parent)) is RuntimeHierarchyEntry found) { depth++; parent = found.ParentId; } return depth; }

        private void DrawInspector()
        {
            GUILayout.BeginVertical(_panelStyle);
            DrawSectionHeader("INSPECTOR", _inspectorFocused ? "FOCUS" : "VIEW");
            GUILayout.Space(4f);
            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
            if (_details == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Select an object from the hierarchy to inspect its components and values.", _mutedStyle);
            }
            else
            {
                GUILayout.BeginHorizontal(_summaryStyle);
                GUILayout.BeginVertical();
                GUILayout.Label(_details.Name, _bodyStyle);
                GUILayout.Label($"Active: {_details.Active}   Tag: {_details.Tag}   Layer: {_details.Layer}", _mutedStyle);
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(_details.Active ? "DEACTIVATE" : "ACTIVATE", _details.Active ? _warningButtonStyle : _primaryButtonStyle, GUILayout.Width(106f), GUILayout.Height(28f))) { RuntimeCommandResult r = _service.Execute(new SetGameObjectActiveCommand { ObjectId = _details.Id, Active = !_details.Active }); _error = r.Message; Refresh(); }
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
                int fieldIndex = 0;
                foreach (RuntimeComponentDescriptor component in _details.Components)
                {
                    if (component.Missing) { GUILayout.Label("MISSING SCRIPT", _messageStyle); continue; }
                    GUILayout.BeginVertical(_componentStyle);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(_expandedComponents.Contains(component.Id.Value) ? "\u25BC" : "\u25B6", _iconButtonStyle, GUILayout.Width(26f), GUILayout.Height(22f))) { if (!_expandedComponents.Remove(component.Id.Value)) _expandedComponents.Add(component.Id.Value); }
                    GUILayout.Label(component.TypeName, _bodyStyle);
                    if (component.HasEnabledState)
                    {
                        GUIStyle stateStyle = _inspectorFocused && fieldIndex == _inspectorCursor ? _selectedRowStyle : component.Enabled ? _successButtonStyle : _buttonStyle;
                        if (GUILayout.Button(component.Enabled ? "ENABLED" : "DISABLED", stateStyle, GUILayout.Width(82f), GUILayout.Height(22f))) ToggleComponent(component);
                        fieldIndex++;
                    }
                    GUILayout.EndHorizontal();
                    if (!_expandedComponents.Contains(component.Id.Value))
                    {
                        GUILayout.EndVertical();
                        GUILayout.Space(4f);
                        continue;
                    }
                    foreach (RuntimeMemberDescriptor member in component.Members)
                    {
                        GUILayout.BeginHorizontal(_inspectorFocused && fieldIndex == _inspectorCursor ? _selectedRowStyle : _rowStyle);
                        GUILayout.Space(28f);
                        GUILayout.Label(member.DisplayName, _mutedStyle, GUILayout.Width(180f));
                        if (_editing && ReferenceEquals(member, _editingMember)) _editValue = GUILayout.TextField(_editValue, _valueFieldStyle, GUILayout.Height(22f));
                        else GUILayout.Label(member.Error ?? member.Value, member.Error == null ? _bodyStyle : _messageStyle);
                        if (!member.ReadOnly && GUILayout.Button("EDIT", _primaryButtonStyle, GUILayout.Width(48f), GUILayout.Height(22f))) BeginEdit(component, member);
                        GUILayout.EndHorizontal();
                        fieldIndex++;
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(4f);
                }
            }
            GUILayout.EndScrollView(); GUILayout.EndVertical();
        }

        private void DrawSectionHeader(string title, string state)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, _sectionStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(state, _badgeStyle, GUILayout.Height(20f));
            GUILayout.EndHorizontal();
        }

        private void EnsureTheme()
        {
            if (_windowStyle != null)
                return;

            Color windowColor = _settings != null ? _settings.BackgroundColor : WindowColor;
            Color focusColor = _settings != null ? _settings.FocusColor : FocusColor;
            Texture2D windowTexture = MakeTexture(windowColor);
            Texture2D cardTexture = MakeTexture(CardColor);
            Texture2D subtleTexture = MakeTexture(Color.Lerp(CardColor, windowColor, 0.38f));
            Texture2D inputTexture = MakeTexture(new Color(0.065f, 0.08f, 0.11f, 1f));
            Texture2D inputFocusTexture = MakeTexture(Color.Lerp(new Color(0.065f, 0.08f, 0.11f, 1f), focusColor, 0.22f));
            Texture2D transparentTexture = MakeTexture(new Color(0f, 0f, 0f, 0f));
            Texture2D hoverTexture = MakeTexture(Color.Lerp(CardColor, focusColor, 0.14f));
            Texture2D selectedTexture = MakeTexture(Color.Lerp(CardColor, focusColor, 0.34f));
            Texture2D selectedHoverTexture = MakeTexture(Color.Lerp(CardColor, focusColor, 0.46f));
            Texture2D primaryTexture = MakeTexture(Color.Lerp(windowColor, focusColor, 0.54f));
            Texture2D primaryHoverTexture = MakeTexture(Color.Lerp(windowColor, focusColor, 0.7f));
            Texture2D primaryActiveTexture = MakeTexture(Color.Lerp(windowColor, focusColor, 0.38f));
            Texture2D successTexture = MakeTexture(Color.Lerp(windowColor, SuccessColor, 0.36f));
            Texture2D successHoverTexture = MakeTexture(Color.Lerp(windowColor, SuccessColor, 0.52f));
            Texture2D warningTexture = MakeTexture(Color.Lerp(windowColor, WarningColor, 0.34f));
            Texture2D warningHoverTexture = MakeTexture(Color.Lerp(windowColor, WarningColor, 0.5f));
            Texture2D messageTexture = MakeTexture(Color.Lerp(windowColor, WarningColor, 0.15f));

            _windowStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(16, 16, 14, 14), normal = { background = windowTexture } };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TextColor } };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = HeaderColor } };
            _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = TextColor }, clipping = TextClipping.Ellipsis };
            _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = MutedColor }, clipping = TextClipping.Ellipsis };
            _footerStyle = new GUIStyle(_mutedStyle) { wordWrap = true };
            _badgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 3, 3), normal = { background = subtleTexture, textColor = HeaderColor } };
            _panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8), normal = { background = cardTexture } };
            _toolbarStyle = new GUIStyle(_panelStyle) { padding = new RectOffset(10, 10, 6, 6) };
            _summaryStyle = new GUIStyle(_panelStyle) { padding = new RectOffset(8, 8, 6, 6), normal = { background = subtleTexture } };
            _searchFieldStyle = CreateTextFieldStyle(inputTexture, inputFocusTexture, 12);
            _valueFieldStyle = CreateTextFieldStyle(inputTexture, inputFocusTexture, 11);
            _buttonStyle = CreateButtonStyle(subtleTexture, hoverTexture, selectedTexture, HeaderColor, 11);
            _primaryButtonStyle = CreateButtonStyle(primaryTexture, primaryHoverTexture, primaryActiveTexture, TextColor, 11);
            _successButtonStyle = CreateButtonStyle(successTexture, successHoverTexture, selectedTexture, TextColor, 10);
            _warningButtonStyle = CreateButtonStyle(warningTexture, warningHoverTexture, selectedTexture, TextColor, 10);
            _iconButtonStyle = CreateButtonStyle(subtleTexture, hoverTexture, selectedTexture, HeaderColor, 12);
            _iconButtonStyle.padding = new RectOffset(2, 2, 1, 1);
            _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, padding = new RectOffset(6, 6, 2, 2), clipping = TextClipping.Ellipsis, normal = { background = transparentTexture, textColor = TextColor }, hover = { background = hoverTexture, textColor = TextColor }, active = { background = selectedTexture, textColor = TextColor }, focused = { background = hoverTexture, textColor = TextColor } };
            _selectedRowStyle = new GUIStyle(_rowStyle) { fontStyle = FontStyle.Bold, normal = { background = selectedTexture, textColor = TextColor }, hover = { background = selectedHoverTexture, textColor = TextColor }, active = { background = primaryActiveTexture, textColor = TextColor }, focused = { background = selectedTexture, textColor = TextColor } };
            _sceneRowStyle = new GUIStyle(_rowStyle) { fontStyle = FontStyle.Bold, normal = { background = transparentTexture, textColor = HeaderColor } };
            _inactiveRowStyle = new GUIStyle(_rowStyle) { normal = { background = transparentTexture, textColor = MutedColor } };
            _componentStyle = new GUIStyle(_panelStyle) { padding = new RectOffset(6, 6, 4, 4), margin = new RectOffset(0, 0, 0, 2), normal = { background = subtleTexture } };
            _messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true, padding = new RectOffset(8, 8, 5, 5), normal = { background = messageTexture, textColor = WarningColor } };
            _resizeHandleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { background = subtleTexture, textColor = MutedColor } };
        }

        private GUIStyle CreateTextFieldStyle(Texture2D normalTexture, Texture2D focusedTexture, int fontSize)
        {
            return new GUIStyle(GUI.skin.textField)
            {
                fontSize = fontSize,
                padding = new RectOffset(8, 8, 5, 5),
                normal = { background = normalTexture, textColor = TextColor },
                hover = { background = focusedTexture, textColor = TextColor },
                active = { background = focusedTexture, textColor = TextColor },
                focused = { background = focusedTexture, textColor = TextColor }
            };
        }

        private GUIStyle CreateButtonStyle(Texture2D normalTexture, Texture2D hoverTexture, Texture2D activeTexture, Color textColor, int fontSize)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
                normal = { background = normalTexture, textColor = textColor },
                hover = { background = hoverTexture, textColor = TextColor },
                active = { background = activeTexture, textColor = TextColor },
                focused = { background = hoverTexture, textColor = TextColor }
            };
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _themeTextures.Add(texture);
            return texture;
        }

        private void DisposeTheme()
        {
            foreach (Texture2D texture in _themeTextures)
                if (texture != null)
                    Destroy(texture);

            _themeTextures.Clear();
            _windowStyle = null;
        }
    }
}

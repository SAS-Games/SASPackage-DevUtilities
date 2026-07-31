using System;
using System.Linq;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector
{
    internal enum RuntimeSceneInspectorPanel
    {
        Search,
        Hierarchy,
        Inspector
    }

    internal sealed class RuntimeSceneInspectorView : IDisposable
    {
        private const int ResizeControlHint = 0x52D38;
        private const string SearchControlName = "RuntimeSceneInspector.Search";
        private const string EditValueControlName = "RuntimeSceneInspector.EditValue";

        private readonly RuntimeSceneInspectorController _controller;
        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly RuntimeSceneInspectorTheme _theme;
        private readonly RuntimeSceneInspectorFontAtlas _fontAtlas = new();
        private readonly RuntimeMaterialShaderInspectorView _materialShaderView;
        private Rect _window = new(80, 60, 1100, 700);
        private Rect _searchFieldRect;
        private Vector2 _pendingWindowSize;
        private Vector2 _hierarchyScroll;
        private Vector2 _inspectorScroll;

        internal RuntimeSceneInspectorView(RuntimeSceneInspectorController controller, RuntimeSceneInspectorSettings settings)
        {
            _controller = controller;
            _settings = settings;
            _theme = new RuntimeSceneInspectorTheme(settings);
            _materialShaderView = new RuntimeMaterialShaderInspectorView(controller, settings, _theme);
        }

        internal void Draw(int windowId)
        {
            if (!_controller.IsOpen)
                return;

            float scale = Mathf.Clamp(_settings.UiScale, 0.75f, 2f);
            float logicalScreenWidth = Screen.width / scale;
            float logicalScreenHeight = Screen.height / scale;
            float minimumWidth = Mathf.Min(650f, logicalScreenWidth);
            float minimumHeight = Mathf.Min(400f, logicalScreenHeight);
            _window.width = Mathf.Clamp(_window.width, minimumWidth, logicalScreenWidth);
            _window.height = Mathf.Clamp(_window.height, minimumHeight, logicalScreenHeight);
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, logicalScreenWidth - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, logicalScreenHeight - _window.height));

            RevealHierarchyCursorIfNeeded();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _theme.EnsureCreated();
            _fontAtlas.ValidateVisibleCharacters(_theme.Font, _theme.BoldFont, _controller);
            Rect windowResult = GUI.Window(windowId, _window, DrawWindow, GUIContent.none, _theme.Window);
            if (_pendingWindowSize.x > 0f && _pendingWindowSize.y > 0f)
            {
                windowResult.size = _pendingWindowSize;
                _pendingWindowSize = Vector2.zero;
            }

            _window = windowResult;
            GUI.matrix = previousMatrix;
        }

        public void Dispose() => _theme.Dispose();

        private void DrawWindow(int id)
        {
            HandleGuiFocusKey();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && _searchFieldRect.Contains(Event.current.mousePosition))
                _controller.SetFocusedPanel(RuntimeSceneInspectorPanel.Search);

            if (_controller.ClearGuiFocus)
            {
                GUIUtility.keyboardControl = 0;
                if (Event.current.type == EventType.Repaint)
                    _controller.ClearGuiFocus = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("RUNTIME SCENE INSPECTOR", _theme.Title);
            GUILayout.Label("LIVE SCENE INSPECTION", _theme.Muted);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_controller.Snapshot == null ? "REFRESHING" : $"{_controller.VisibleEntries.Count} ITEMS", _theme.Badge, GUILayout.Height(22f));
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            DrawPanelTabs();
            GUILayout.Space(6f);
            DrawSearch();
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            DrawHierarchy();
            GUILayout.Space(8f);
            DrawInspector();
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            DrawFooter();
            DrawWindowControls();
        }

        private void DrawSearch()
        {
            GUILayout.BeginHorizontal(_theme.Toolbar);
            GUILayout.Label("SEARCH", _theme.Section, GUILayout.Width(58f));
            int previousKeyboardControl = GUIUtility.keyboardControl;
            GUI.SetNextControlName(SearchControlName);
            string next = GUILayout.TextField(_controller.Search, _theme.SearchField, GUILayout.Height(28f));
            if (Event.current.type == EventType.Repaint)
                _searchFieldRect = GUILayoutUtility.GetLastRect();
            else if (!_controller.IsSearchFocused && Event.current.rawType == EventType.MouseDown && Event.current.button == 0 && GUIUtility.keyboardControl != previousKeyboardControl && GUI.GetNameOfFocusedControl() == SearchControlName)
                _controller.SetFocusedPanel(RuntimeSceneInspectorPanel.Search);

            if (next != _controller.Search)
                _controller.SetSearch(next);

            if (_controller.IsSearchFocused && _controller.FocusSearchField)
            {
                GUI.FocusControl(SearchControlName);
                if (Event.current.type == EventType.Repaint)
                    _controller.FocusSearchField = false;
            }

            if (GUILayout.Button("CLEAR", _theme.Button, GUILayout.Width(64f), GUILayout.Height(28f)))
            {
                _controller.SetFocusedPanel(RuntimeSceneInspectorPanel.Search);
                _controller.SetSearch(string.Empty);
            }

            GUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            if (!string.IsNullOrEmpty(_controller.Error))
                GUILayout.Label($"STATUS  {_controller.Error}", _theme.Message);
            else if (_controller.IsEditing)
                GUILayout.Label("EDITING  Left/Right move the text cursor  /  Enter/A or SAVE applies  /  Esc/B cancels", _theme.Footer);
            else if (_controller.IsSearchFocused)
                GUILayout.Label("SEARCH  Type to filter  /  X clears  /  Tab/RB next panel  /  Shift+Tab/LB previous", _theme.Footer);
            else if (_controller.IsInspectorFocused)
                GUILayout.Label("INSPECTOR  Arrows navigate  /  Enter/A acts  /  Space/X toggles  /  Tab/RB next panel  /  Shift+Tab/LB previous", _theme.Footer);
            else
                GUILayout.Label("HIERARCHY  Arrows navigate  /  Enter/A inspects  /  Space/X toggles  /  Tab/RB next panel  /  Shift+Tab/LB previous", _theme.Footer);
        }

        private void DrawWindowControls()
        {
            GUI.DragWindow(new Rect(0f, 0f, _window.width, 55f));
            Rect grip = new(_window.width - 18, _window.height - 18, 18, 18);
            GUI.Box(grip, "\u2198", _theme.ResizeHandle);
            Event current = Event.current;
            int resizeControlId = GUIUtility.GetControlID(ResizeControlHint, FocusType.Passive, grip);
            EventType resizeEvent = current.GetTypeForControl(resizeControlId);
            if (resizeEvent == EventType.MouseDown && current.button == 0 && grip.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = resizeControlId;
                current.Use();
            }
            else if (resizeEvent == EventType.MouseDrag && GUIUtility.hotControl == resizeControlId)
            {
                float scale = Mathf.Clamp(_settings.UiScale, 0.75f, 2f);
                float maximumWidth = Screen.width / scale - _window.x;
                float maximumHeight = Screen.height / scale - _window.y;
                Vector2 nextSize = _window.size + current.delta;
                _pendingWindowSize = new Vector2(Mathf.Clamp(nextSize.x, Mathf.Min(650f, maximumWidth), maximumWidth), Mathf.Clamp(nextSize.y, Mathf.Min(400f, maximumHeight), maximumHeight));
                current.Use();
            }
            else if (resizeEvent == EventType.MouseUp && GUIUtility.hotControl == resizeControlId)
            {
                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

        private void DrawPanelTabs()
        {
            GUILayout.BeginHorizontal(_theme.Toolbar);
            DrawPanelTab("SEARCH", RuntimeSceneInspectorPanel.Search, 88f);
            DrawPanelTab("HIERARCHY", RuntimeSceneInspectorPanel.Hierarchy, 104f);
            DrawPanelTab("INSPECTOR", RuntimeSceneInspectorPanel.Inspector, 96f);
            GUILayout.FlexibleSpace();
            GUILayout.Label("TAB / RB  NEXT", _theme.Muted, GUILayout.Height(28f));
            GUILayout.EndHorizontal();
        }

        private void DrawPanelTab(string label, RuntimeSceneInspectorPanel panel, float width)
        {
            GUIStyle style = _controller.FocusedPanel == panel ? _theme.PrimaryButton : _theme.Button;
            if (GUILayout.Button(label, style, GUILayout.Width(width), GUILayout.Height(28f)))
                _controller.SetFocusedPanel(panel);
        }

        private void HandleGuiFocusKey()
        {
            Event current = Event.current;
            if ((current.type == EventType.KeyDown || current.type == EventType.KeyUp) && current.keyCode == KeyCode.Tab)
            {
                current.Use();
                return;
            }

            if (current.type != EventType.KeyDown || _controller.IsEditing)
                return;

            bool controlF = current.keyCode == KeyCode.F && current.control;
            bool slash = current.keyCode == KeyCode.Slash || current.character == '/';
            if (controlF || slash && !_controller.IsSearchFocused)
            {
                _controller.SetFocusedPanel(RuntimeSceneInspectorPanel.Search);
                current.Use();
                return;
            }

            if (_controller.IsSearchFocused)
                return;

            if (current.keyCode == KeyCode.UpArrow || current.keyCode == KeyCode.DownArrow || current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.Home || current.keyCode == KeyCode.End || current.keyCode == KeyCode.PageUp || current.keyCode == KeyCode.PageDown)
                current.Use();
        }

        private void DrawHierarchy()
        {
            GUILayout.BeginVertical(_theme.Panel, GUILayout.Width(_window.width * _settings.HierarchyPanelWidth));
            DrawSectionHeader("HIERARCHY", _controller.IsHierarchyFocused ? "FOCUS" : "VIEW");
            GUILayout.Space(4f);
            _hierarchyScroll = GUILayout.BeginScrollView(_hierarchyScroll);
            for (int i = 0; i < _controller.VisibleEntries.Count; i++)
            {
                RuntimeHierarchyEntry entry = _controller.VisibleEntries[i];
                int depth = Depth(entry);
                string arrow = _controller.HasHierarchyChildren(entry) ? (_controller.ExpandedHierarchy.Contains(entry.Id.Value) ? "\u25BC " : "\u25B6 ") : "  ";
                string inactive = entry.Kind == RuntimeHierarchyKind.GameObject && !entry.ActiveSelf ? " (inactive)" : string.Empty;
                GUIStyle rowStyle = _controller.IsHierarchyFocused && i == _controller.HierarchyCursor ? _theme.SelectedRow : entry.Kind == RuntimeHierarchyKind.Scene ? _theme.SceneRow : !entry.ActiveSelf ? _theme.InactiveRow : _theme.Row;
                if (GUILayout.Button(new string(' ', depth * 3) + arrow + entry.Name + inactive, rowStyle, GUILayout.Height(20f)))
                    _controller.ActivateHierarchyRow(i);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private int Depth(RuntimeHierarchyEntry entry)
        {
            int depth = 0;
            RuntimeObjectId parent = entry.ParentId;
            while (parent.IsValid && _controller.Snapshot.Entries.FirstOrDefault(item => item.Id.Equals(parent)) is { } found)
            {
                depth++;
                parent = found.ParentId;
            }

            return depth;
        }

        private void DrawInspector()
        {
            GUILayout.BeginVertical(_theme.Panel);
            DrawSectionHeader("INSPECTOR", _controller.IsInspectorFocused ? "FOCUS" : "VIEW");
            GUILayout.Space(4f);
            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
            RuntimeObjectDetails details = _controller.Details;
            if (details == null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Select an object from the hierarchy to inspect its components and values.", _theme.Muted);
            }
            else
                DrawInspectorContents(details);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawInspectorContents(RuntimeObjectDetails details)
        {
            int fieldIndex = 0;
            int activeRowIndex = fieldIndex++;
            GUILayout.BeginHorizontal(_theme.Summary);
            GUILayout.BeginVertical();
            GUILayout.Label(details.Name, _theme.Body);
            GUILayout.Label($"Active: {details.Active}   Tag: {details.Tag}   Layer: {details.Layer}", _theme.Muted);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUIStyle activeButtonStyle = _controller.IsInspectorFocused && _controller.InspectorCursor == activeRowIndex ? _theme.SelectedRow : details.Active ? _theme.WarningButton : _theme.PrimaryButton;
            bool activeClicked = GUILayout.Button(details.Active ? "DEACTIVATE" : "ACTIVATE", activeButtonStyle, GUILayout.Width(106f), GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            RevealInspectorCursorIfNeeded(activeRowIndex);
            if (activeClicked)
                _controller.ActivateInspectedObjectRow(activeRowIndex);

            GUILayout.Space(5f);
            foreach (RuntimeComponentDescriptor component in details.Components)
            {
                if (component.Missing)
                {
                    GUILayout.Label("MISSING SCRIPT", _theme.Message);
                    continue;
                }

                DrawComponent(component, ref fieldIndex);
            }

            _materialShaderView.Draw(details.MaterialsAndShaders, ref fieldIndex);
        }

        private void DrawComponent(RuntimeComponentDescriptor component, ref int fieldIndex)
        {
            GUILayout.BeginVertical(_theme.Component);
            int componentRowIndex = fieldIndex++;
            bool hasMembers = RuntimeSceneInspectorController.HasInspectorMembers(component);
            GUIStyle componentRowStyle = _controller.IsInspectorFocused && _controller.InspectorCursor == componentRowIndex ? _theme.SelectedRow : _theme.Row;
            GUILayout.BeginHorizontal(componentRowStyle);
            bool foldoutClicked = false;
            if (hasMembers)
                foldoutClicked = GUILayout.Button(_controller.ExpandedComponents.Contains(component.Id.Value) ? "\u25BC" : "\u25B6", _theme.IconButton, GUILayout.Width(26f), GUILayout.Height(22f));
            else
                GUILayout.Space(26f);
            GUILayout.Label(component.TypeName, _theme.Body);
            bool toggleClicked = false;
            if (component.HasEnabledState)
            {
                GUIStyle stateStyle = _controller.IsInspectorFocused && componentRowIndex == _controller.InspectorCursor ? _theme.SelectedRow : component.Enabled ? _theme.SuccessButton : _theme.Button;
                toggleClicked = GUILayout.Button(component.Enabled ? "ENABLED" : "DISABLED", stateStyle, GUILayout.Width(82f), GUILayout.Height(22f));
            }

            GUILayout.EndHorizontal();
            RevealInspectorCursorIfNeeded(componentRowIndex);
            if (foldoutClicked)
                _controller.ToggleComponentFoldout(component, componentRowIndex);
            if (toggleClicked)
                _controller.ToggleComponentFromView(component, componentRowIndex);

            if (!string.IsNullOrWhiteSpace(component.StatusMessage))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(32f);
                GUILayout.Label(component.StatusMessage, _theme.Muted);
                GUILayout.EndHorizontal();
            }

            if (_controller.ExpandedComponents.Contains(component.Id.Value))
            {
                foreach (RuntimeMemberDescriptor member in component.Members ?? Array.Empty<RuntimeMemberDescriptor>())
                    DrawMember(component, member, fieldIndex++);
            }

            GUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void DrawMember(RuntimeComponentDescriptor component, RuntimeMemberDescriptor member, int fieldIndex)
        {
            GUILayout.BeginHorizontal(_controller.IsInspectorFocused && fieldIndex == _controller.InspectorCursor ? _theme.SelectedRow : _theme.Row);
            GUILayout.Space(28f);
            GUILayout.Label(member.DisplayName, _theme.Muted, GUILayout.Width(180f));
            bool isEditing = _controller.IsEditingMember(component, member);
            if (isEditing)
            {
                GUI.SetNextControlName(EditValueControlName);
                _controller.EditValue = GUILayout.TextField(_controller.EditValue, _theme.ValueField, GUILayout.Height(22f));
                if (_controller.FocusEditField)
                {
                    GUI.FocusControl(EditValueControlName);
                    if (Event.current.type == EventType.Repaint)
                        _controller.FocusEditField = false;
                }
            }
            else
                GUILayout.Label(member.Error ?? member.Value, member.Error == null ? _theme.Body : _theme.Message);

            if (!member.ReadOnly && isEditing)
            {
                if (GUILayout.Button("SAVE", _theme.PrimaryButton, GUILayout.Width(48f), GUILayout.Height(22f)))
                    _controller.CommitEdit();
                if (GUILayout.Button("X", _theme.Button, GUILayout.Width(24f), GUILayout.Height(22f)))
                    _controller.CancelEdit();
            }
            else if (!member.ReadOnly && GUILayout.Button("EDIT", _theme.PrimaryButton, GUILayout.Width(48f), GUILayout.Height(22f)))
                _controller.BeginEditFromView(component, member, fieldIndex);

            GUILayout.EndHorizontal();
            RevealInspectorCursorIfNeeded(fieldIndex);
        }

        private void RevealHierarchyCursorIfNeeded()
        {
            if (!_controller.RevealHierarchyCursor)
                return;

            const float rowHeight = 20f;
            float viewportHeight = Mathf.Max(100f, _window.height - 230f);
            float rowTop = _controller.HierarchyCursor * rowHeight;
            float rowBottom = rowTop + rowHeight;
            if (rowTop < _hierarchyScroll.y)
                _hierarchyScroll.y = rowTop;
            else if (rowBottom > _hierarchyScroll.y + viewportHeight)
                _hierarchyScroll.y = Mathf.Max(0f, rowBottom - viewportHeight);
            _controller.RevealHierarchyCursor = false;
        }

        private void RevealInspectorCursorIfNeeded(int fieldIndex)
        {
            if (!_controller.RevealInspectorCursor || !_controller.IsInspectorFocused || fieldIndex != _controller.InspectorCursor || Event.current.type != EventType.Repaint)
                return;

            GUI.ScrollTo(GUILayoutUtility.GetLastRect());
            _controller.RevealInspectorCursor = false;
        }

        private void DrawSectionHeader(string title, string state)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, _theme.Section);
            GUILayout.FlexibleSpace();
            GUILayout.Label(state, _theme.Badge, GUILayout.Height(20f));
            GUILayout.EndHorizontal();
        }
    }
}

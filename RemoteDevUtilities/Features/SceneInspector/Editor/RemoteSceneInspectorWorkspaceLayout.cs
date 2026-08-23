using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    /// <summary>
    /// Responsive three-pane layout shared by live capture and recorded-frame inspection.
    /// Wide windows show all panes with draggable splitters, medium windows keep the hierarchy
    /// beside a switchable detail pane, and narrow windows show one pane at a time.
    /// </summary>
    internal sealed class RemoteSceneInspectorWorkspaceLayout
    {
        private const float WideBreakpoint = 1100f;
        private const float MediumBreakpoint = 760f;
        private const float SplitterWidth = 5f;
        private const float MinimumHierarchyWidth = 190f;
        private const float MinimumContentWidth = 280f;
        private const float MinimumInspectorWidth = 260f;
        private const int SplitterControlHint = 0x525349;

        private float _hierarchyRatio = 0.24f;
        private float _contentRatio = 0.4f;
        private int _mediumDetailPane;
        private int _compactPane = 1;
        private int _draggingSplitter = -1;
        private float _dragStartX;
        private float _dragStartHierarchyRatio;
        private float _dragStartContentRatio;

        internal void ShowInspector()
        {
            _mediumDetailPane = 1;
            _compactPane = 2;
        }

        internal void Draw(Rect windowRect, float reservedHeight,
            string hierarchyTitle, string contentTitle, string inspectorTitle,
            Action<float, float> drawHierarchy,
            Action<float, float> drawContent,
            Action<float, float> drawInspector)
        {
            float availableWidth = Mathf.Max(320f, windowRect.width - 24f);
            float columnHeight = Mathf.Max(300f, windowRect.height - reservedHeight);
            if (availableWidth >= WideBreakpoint)
            {
                DrawWide(availableWidth, columnHeight,
                    hierarchyTitle, contentTitle, inspectorTitle,
                    drawHierarchy, drawContent, drawInspector);
            }
            else if (availableWidth >= MediumBreakpoint)
            {
                DrawMedium(availableWidth, columnHeight,
                    hierarchyTitle, contentTitle, inspectorTitle,
                    drawHierarchy, drawContent, drawInspector);
            }
            else
            {
                DrawCompact(availableWidth, columnHeight,
                    hierarchyTitle, contentTitle, inspectorTitle,
                    drawHierarchy, drawContent, drawInspector);
            }
        }

        private void DrawWide(float availableWidth, float height,
            string hierarchyTitle, string contentTitle, string inspectorTitle,
            Action<float, float> drawHierarchy,
            Action<float, float> drawContent,
            Action<float, float> drawInspector)
        {
            float paneWidth = Mathf.Max(1f, availableWidth - SplitterWidth * 2f);
            float hierarchyWidth = Mathf.Clamp(paneWidth * _hierarchyRatio,
                MinimumHierarchyWidth, paneWidth - MinimumContentWidth - MinimumInspectorWidth);
            float contentWidth = Mathf.Clamp(paneWidth * _contentRatio,
                MinimumContentWidth, paneWidth - hierarchyWidth - MinimumInspectorWidth);
            float inspectorWidth = Mathf.Max(MinimumInspectorWidth,
                paneWidth - hierarchyWidth - contentWidth);
            _hierarchyRatio = hierarchyWidth / paneWidth;
            _contentRatio = contentWidth / paneWidth;

            EditorGUILayout.BeginHorizontal();
            DrawPane(hierarchyTitle, hierarchyWidth, height, drawHierarchy);
            DrawSplitter(0, paneWidth, height);
            DrawPane(contentTitle, contentWidth, height, drawContent);
            DrawSplitter(1, paneWidth, height);
            DrawPane(inspectorTitle, inspectorWidth, height, drawInspector);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMedium(float availableWidth, float height,
            string hierarchyTitle, string contentTitle, string inspectorTitle,
            Action<float, float> drawHierarchy,
            Action<float, float> drawContent,
            Action<float, float> drawInspector)
        {
            float paneWidth = Mathf.Max(1f, availableWidth - SplitterWidth);
            float hierarchyWidth = Mathf.Clamp(paneWidth * _hierarchyRatio,
                MinimumHierarchyWidth, paneWidth - MinimumContentWidth);
            float detailWidth = Mathf.Max(MinimumContentWidth, paneWidth - hierarchyWidth);
            _hierarchyRatio = hierarchyWidth / paneWidth;

            EditorGUILayout.BeginHorizontal();
            DrawPane(hierarchyTitle, hierarchyWidth, height, drawHierarchy);
            DrawSplitter(0, paneWidth, height);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox,
                GUILayout.Width(detailWidth), GUILayout.Height(height));
            _mediumDetailPane = GUILayout.Toolbar(_mediumDetailPane,
                new[] { contentTitle, inspectorTitle }, EditorStyles.toolbarButton);
            if (_mediumDetailPane == 0)
                drawContent?.Invoke(Mathf.Max(1f, detailWidth - 12f), height - 24f);
            else
                drawInspector?.Invoke(Mathf.Max(1f, detailWidth - 12f), height - 24f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCompact(float availableWidth, float height,
            string hierarchyTitle, string contentTitle, string inspectorTitle,
            Action<float, float> drawHierarchy,
            Action<float, float> drawContent,
            Action<float, float> drawInspector)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox,
                GUILayout.Width(availableWidth), GUILayout.Height(height));
            _compactPane = GUILayout.Toolbar(_compactPane,
                new[] { hierarchyTitle, contentTitle, inspectorTitle }, EditorStyles.toolbarButton);
            float contentWidth = Mathf.Max(1f, availableWidth - 12f);
            float contentHeight = height - 24f;
            switch (_compactPane)
            {
                case 0:
                    drawHierarchy?.Invoke(contentWidth, contentHeight);
                    break;
                case 2:
                    drawInspector?.Invoke(contentWidth, contentHeight);
                    break;
                default:
                    drawContent?.Invoke(contentWidth, contentHeight);
                    break;
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawPane(string title, float width, float height,
            Action<float, float> drawer)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox,
                GUILayout.Width(width), GUILayout.Height(height));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            drawer?.Invoke(Mathf.Max(1f, width - 12f), height - 24f);
            EditorGUILayout.EndVertical();
        }

        private void DrawSplitter(int index, float availableWidth, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(SplitterWidth, height,
                GUILayout.Width(SplitterWidth), GUILayout.Height(height));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.Repaint)
            {
                Color color = EditorGUIUtility.isProSkin
                    ? new Color(0.28f, 0.28f, 0.28f)
                    : new Color(0.68f, 0.68f, 0.68f);
                EditorGUI.DrawRect(new Rect(rect.center.x - 0.5f, rect.y, 1f, rect.height), color);
            }

            int controlId = GUIUtility.GetControlID(SplitterControlHint + index,
                FocusType.Passive, rect);
            Event current = Event.current;
            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when current.button == 0 && rect.Contains(current.mousePosition):
                    GUIUtility.hotControl = controlId;
                    _draggingSplitter = index;
                    _dragStartX = current.mousePosition.x;
                    _dragStartHierarchyRatio = _hierarchyRatio;
                    _dragStartContentRatio = _contentRatio;
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == controlId &&
                                               _draggingSplitter == index:
                    float deltaRatio = (current.mousePosition.x - _dragStartX) /
                                       Mathf.Max(1f, availableWidth);
                    if (index == 0)
                        _hierarchyRatio = _dragStartHierarchyRatio + deltaRatio;
                    else
                        _contentRatio = _dragStartContentRatio + deltaRatio;
                    GUI.changed = true;
                    current.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    _draggingSplitter = -1;
                    current.Use();
                    break;
            }
        }
    }

    internal sealed class RemoteSceneInspectorSelectionBreadcrumb
    {
        private long _hierarchyRevision = long.MinValue;
        private long _objectId = long.MinValue;
        private int _sessionGeneration = int.MinValue;
        private RemoteRuntimeSceneInspectorClient _client;
        private string _path = string.Empty;

        internal void Draw(RemoteRuntimeSceneInspectorClient client, string contextLabel)
        {
            string path = ResolvePath(client);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var context = new GUIContent(contextLabel);
            float contextWidth = Mathf.Clamp(
                EditorStyles.miniBoldLabel.CalcSize(context).x + 8f, 72f, 160f);
            GUILayout.Label(context, EditorStyles.miniBoldLabel, GUILayout.Width(contextWidth));
            GUILayout.Label(new GUIContent(path, path), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private string ResolvePath(RemoteRuntimeSceneInspectorClient client)
        {
            long revision = client?.Hierarchy?.Revision ?? long.MinValue;
            long objectId = client?.InspectionObjectId ?? 0;
            int sessionGeneration = client?.SessionGeneration ?? int.MinValue;
            if (ReferenceEquals(client, _client) && revision == _hierarchyRevision &&
                objectId == _objectId && sessionGeneration == _sessionGeneration)
                return _path;

            _client = client;
            _hierarchyRevision = revision;
            _objectId = objectId;
            _sessionGeneration = sessionGeneration;
            _path = BuildPath(client?.Hierarchy, objectId,
                client?.Inspection?.Details?.Name);
            return _path;
        }

        internal static string BuildPath(RemoteSceneInspectorHierarchyResponse hierarchy,
            long objectId, string fallbackName = null)
        {
            if (objectId <= 0)
                return "No object selected";

            var entries = new Dictionary<long, RemoteHierarchyEntry>();
            foreach (RemoteHierarchyEntry entry in
                     hierarchy?.Entries ?? Array.Empty<RemoteHierarchyEntry>())
            {
                if (entry != null)
                    entries[entry.Id] = entry;
            }

            if (!entries.TryGetValue(objectId, out RemoteHierarchyEntry current))
                return string.IsNullOrWhiteSpace(fallbackName)
                    ? $"Object {objectId}"
                    : fallbackName;

            var segments = new List<string>();
            var visited = new HashSet<long>();
            while (current != null && visited.Add(current.Id))
            {
                if (!string.IsNullOrWhiteSpace(current.Name))
                    segments.Add(current.Name);
                if (current.ParentId <= 0 || !entries.TryGetValue(current.ParentId, out current))
                    break;
            }

            segments.Reverse();
            return segments.Count == 0 ? $"Object {objectId}" : string.Join(" / ", segments);
        }
    }
}

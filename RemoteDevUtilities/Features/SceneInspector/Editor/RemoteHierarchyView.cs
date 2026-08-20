using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteHierarchyView
    {
        private readonly HashSet<long> _expanded = new();
        private readonly Dictionary<long, List<RemoteHierarchyEntry>> _children = new();
        private string _search = string.Empty;
        private long _cachedRevision = long.MinValue;

        public long SelectedObjectId { get; private set; }

        public bool SelectAndReveal(long objectId, RemoteSceneInspectorHierarchyResponse hierarchy)
        {
            if (objectId <= 0 || hierarchy?.Entries == null)
                return false;

            EnsureLookup(hierarchy);
            var byId = new Dictionary<long, RemoteHierarchyEntry>();
            foreach (RemoteHierarchyEntry entry in hierarchy.Entries)
                byId[entry.Id] = entry;
            if (!byId.TryGetValue(objectId, out RemoteHierarchyEntry selected) || selected.Kind == 0)
                return false;

            _search = string.Empty;
            for (long parentId = selected.ParentId; parentId > 0 && byId.TryGetValue(parentId, out RemoteHierarchyEntry parent); parentId = parent.ParentId)
                _expanded.Add(parent.Id);
            SelectedObjectId = objectId;
            return true;
        }

        public void Draw(RemoteRuntimeSceneInspectorClient client)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = GUILayout.TextField(_search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(100f));
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                client.Refresh(SelectedObjectId);
            EditorGUILayout.EndHorizontal();

            RemoteSceneInspectorHierarchyResponse hierarchy = client.Hierarchy;
            EnsureLookup(hierarchy);

            if (hierarchy.Entries == null || hierarchy.Entries.Length == 0)
            {
                EditorGUILayout.LabelField("No hierarchy data has been received.", EditorStyles.centeredGreyMiniLabel);
            }
            else if (string.IsNullOrWhiteSpace(_search))
            {
                DrawChildren(client, 0L, 0);
            }
            else
            {
                DrawSearchResults(client, hierarchy.Entries);
            }
        }

        private void EnsureLookup(RemoteSceneInspectorHierarchyResponse hierarchy)
        {
            if (hierarchy == null || hierarchy.Revision == _cachedRevision)
                return;

            _cachedRevision = hierarchy.Revision;
            _children.Clear();
            RemoteHierarchyEntry[] entries = hierarchy.Entries ?? Array.Empty<RemoteHierarchyEntry>();
            foreach (RemoteHierarchyEntry entry in entries)
            {
                long parentId = entry.Kind == 0 ? 0L : entry.ParentId;
                if (!_children.TryGetValue(parentId, out List<RemoteHierarchyEntry> list))
                {
                    list = new List<RemoteHierarchyEntry>();
                    _children[parentId] = list;
                }

                list.Add(entry);
            }
        }

        private void DrawChildren(RemoteRuntimeSceneInspectorClient client, long parentId, int depth)
        {
            if (!_children.TryGetValue(parentId, out List<RemoteHierarchyEntry> entries))
                return;

            foreach (RemoteHierarchyEntry entry in entries)
            {
                bool hasChildren = _children.ContainsKey(entry.Id);
                DrawRow(client, entry, depth, hasChildren);
                if (hasChildren && _expanded.Contains(entry.Id))
                    DrawChildren(client, entry.Id, depth + 1);
            }
        }

        private void DrawSearchResults(RemoteRuntimeSceneInspectorClient client, RemoteHierarchyEntry[] entries)
        {
            string search = _search.Trim();
            foreach (RemoteHierarchyEntry entry in entries)
            {
                if (entry.Kind == 0 || (entry.Name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || ContainsComponent(entry.ComponentTypeNames, search))
                    DrawRow(client, entry, 0, false);
            }
        }

        private void DrawRow(RemoteRuntimeSceneInspectorClient client, RemoteHierarchyEntry entry, int depth, bool hasChildren)
        {
            EditorGUILayout.BeginHorizontal(entry.Id == SelectedObjectId ? "SelectionRect" : GUIStyle.none);
            GUILayout.Space(depth * 14f);

            if (hasChildren)
            {
                bool expanded = _expanded.Contains(entry.Id);
                bool next = GUILayout.Toggle(expanded, GUIContent.none, EditorStyles.foldout, GUILayout.Width(13f));
                if (next != expanded)
                {
                    if (next)
                        _expanded.Add(entry.Id);
                    else
                        _expanded.Remove(entry.Id);
                }
            }
            else
            {
                GUILayout.Space(13f);
            }

            string label = entry.Kind == 0 ? $"Scene: {entry.Name}" : entry.Name;
            Color previous = GUI.contentColor;
            if (!entry.ActiveInHierarchy)
                GUI.contentColor = new Color(0.55f, 0.55f, 0.55f);
            if (GUILayout.Button(label, EditorStyles.label))
            {
                if (entry.Kind == 0)
                {
                    if (_expanded.Contains(entry.Id))
                        _expanded.Remove(entry.Id);
                    else
                        _expanded.Add(entry.Id);
                }
                else
                {
                    SelectedObjectId = entry.Id;
                    client.Inspect(entry.Id);
                }
            }

            GUI.contentColor = previous;
            EditorGUILayout.EndHorizontal();
        }

        private static bool ContainsComponent(string[] names, string search)
        {
            if (names == null)
                return false;

            foreach (string name in names)
            {
                if ((name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    return true;
            }

            return false;
        }
    }
}

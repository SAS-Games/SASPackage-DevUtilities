using System;
using System.Collections.Generic;
using System.Linq;
using SAS.Utilities.RuntimeDebugger.Core;
using Unity.Profiling;
using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger
{
    internal enum RuntimeDebuggerNavigationCommand
    {
        Up,
        Down,
        Left,
        Right,
        Home,
        End,
        PageUp,
        PageDown
    }

    internal sealed class RuntimeDebuggerHierarchyController
    {
        private static readonly ProfilerMarker RebuildMarker = new("RuntimeDebugger.UI.Rebuild");
        private readonly IRuntimeDebugger _service;
        private readonly HashSet<long> _expanded = new();
        private readonly HashSet<long> _knownScenes = new();
        private readonly List<RuntimeHierarchyEntry> _visible = new();
        private RuntimeHierarchySnapshot _snapshot;
        private string _search = string.Empty;
        private int _cursor;

        internal RuntimeDebuggerHierarchyController(IRuntimeDebugger service) => _service = service;

        internal RuntimeHierarchySnapshot Snapshot => _snapshot;
        internal List<RuntimeHierarchyEntry> VisibleEntries => _visible;
        internal HashSet<long> ExpandedEntries => _expanded;
        internal string Search => _search;
        internal int Cursor => _cursor;
        internal bool RevealCursor { get; set; }

        internal void Refresh()
        {
            _service.RefreshHierarchy();
            _snapshot = _service.GetHierarchySnapshot();
            foreach (RuntimeHierarchyEntry scene in _snapshot.Entries.Where(item => item.Kind == RuntimeHierarchyKind.Scene))
            {
                if (_knownScenes.Add(scene.Id.Value))
                    _expanded.Add(scene.Id.Value);
            }

            RebuildVisible();
        }

        internal void SetSearch(string value)
        {
            _search = value ?? string.Empty;
            RebuildVisible();
        }

        internal void Navigate(RuntimeDebuggerNavigationCommand command)
        {
            if (_visible.Count == 0)
                return;

            int previousCursor = _cursor;
            RuntimeHierarchyEntry entry = _visible[_cursor];
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
                    _cursor = _visible.Count - 1;
                    break;
                case RuntimeDebuggerNavigationCommand.PageUp:
                    _cursor -= 12;
                    break;
                case RuntimeDebuggerNavigationCommand.PageDown:
                    _cursor += 12;
                    break;
                case RuntimeDebuggerNavigationCommand.Right:
                    NavigateRight(entry);
                    break;
                case RuntimeDebuggerNavigationCommand.Left:
                    NavigateLeft(entry);
                    break;
            }

            _cursor = Mathf.Clamp(_cursor, 0, _visible.Count - 1);
            if (_cursor != previousCursor)
                RevealCursor = true;
        }

        internal RuntimeObjectId ActivateRow(int index)
        {
            if (index < 0 || index >= _visible.Count)
                return default;

            _cursor = index;
            RuntimeHierarchyEntry entry = _visible[index];
            if (entry.Kind == RuntimeHierarchyKind.GameObject)
                return entry.Id;

            if (!_expanded.Remove(entry.Id.Value))
                _expanded.Add(entry.Id.Value);
            RebuildVisible();
            return default;
        }

        internal RuntimeObjectId CurrentGameObjectId()
        {
            if (_visible.Count == 0)
                return default;
            RuntimeHierarchyEntry entry = _visible[_cursor];
            return entry.Kind == RuntimeHierarchyKind.GameObject ? entry.Id : default;
        }

        internal RuntimeCommandResult ToggleCurrentActive()
        {
            if (_visible.Count == 0)
                return RuntimeCommandResult.Fail("No hierarchy object is selected.");
            RuntimeHierarchyEntry entry = _visible[_cursor];
            if (entry.Kind != RuntimeHierarchyKind.GameObject)
                return RuntimeCommandResult.Fail("Scene activation cannot be changed.");
            return _service.Execute(new SetGameObjectActiveCommand { ObjectId = entry.Id, Active = !entry.ActiveSelf });
        }

        internal bool HasChildren(RuntimeHierarchyEntry entry) => entry.Kind == RuntimeHierarchyKind.Scene || _snapshot != null && _snapshot.Entries.Any(item => item.ParentId.Equals(entry.Id));

        private void NavigateRight(RuntimeHierarchyEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(_search))
            {
                if (_cursor + 1 < _visible.Count && _visible[_cursor + 1].ParentId.Equals(entry.Id))
                    _cursor++;
                return;
            }

            if (!HasChildren(entry))
            {
                _expanded.Remove(entry.Id.Value);
                return;
            }

            if (_expanded.Add(entry.Id.Value))
                RebuildVisible();
            else if (_cursor + 1 < _visible.Count && _visible[_cursor + 1].ParentId.Equals(entry.Id))
                _cursor++;
        }

        private void NavigateLeft(RuntimeHierarchyEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(_search))
            {
                int filteredParent = _visible.FindIndex(item => item.Id.Equals(entry.ParentId));
                if (filteredParent >= 0)
                    _cursor = filteredParent;
                return;
            }

            if (HasChildren(entry) && _expanded.Remove(entry.Id.Value))
            {
                RebuildVisible();
                return;
            }

            _expanded.Remove(entry.Id.Value);
            int parent = _visible.FindIndex(item => item.Id.Equals(entry.ParentId));
            if (parent >= 0)
                _cursor = parent;
        }

        private void RebuildVisible()
        {
            using (RebuildMarker.Auto())
            {
                int previousCursor = _cursor;
                RuntimeObjectId previousCursorId = _cursor >= 0 && _cursor < _visible.Count ? _visible[_cursor].Id : default;
                _visible.Clear();
                if (_snapshot == null)
                    return;

                var byId = _snapshot.Entries.ToDictionary(item => item.Id.Value);
                HashSet<long> matches = BuildSearchMatches(byId);
                foreach (RuntimeHierarchyEntry item in _snapshot.Entries)
                {
                    if (matches != null && !matches.Contains(item.Id.Value))
                        continue;
                    if (matches == null && item.Kind == RuntimeHierarchyKind.GameObject && byId.TryGetValue(item.ParentId.Value, out RuntimeHierarchyEntry parent) && !_expanded.Contains(parent.Id.Value))
                        continue;
                    if (matches == null && HasCollapsedAncestor(item, byId))
                        continue;
                    _visible.Add(item);
                }

                int restoredCursor = previousCursorId.IsValid ? _visible.FindIndex(item => item.Id.Equals(previousCursorId)) : -1;
                _cursor = restoredCursor >= 0 ? restoredCursor : Mathf.Clamp(previousCursor, 0, Mathf.Max(0, _visible.Count - 1));
                if (_cursor != previousCursor)
                    RevealCursor = true;
            }
        }

        private HashSet<long> BuildSearchMatches(Dictionary<long, RuntimeHierarchyEntry> byId)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return null;

            var matches = new HashSet<long>();
            foreach (RuntimeHierarchyEntry item in _snapshot.Entries)
            {
                if ((item.Name?.IndexOf(_search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (item.ComponentTypeNames?.Any(
                        type => type.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) ?? false))
                    for (RuntimeHierarchyEntry current = item;
                         current != null && matches.Add(current.Id.Value) &&
                         byId.TryGetValue(current.ParentId.Value, out current);)
                    {
                    }
            }

            return matches;
        }

        private bool HasCollapsedAncestor(RuntimeHierarchyEntry item, Dictionary<long, RuntimeHierarchyEntry> byId)
        {
            while (byId.TryGetValue(item.ParentId.Value, out RuntimeHierarchyEntry parent))
            {
                if (!_expanded.Contains(parent.Id.Value))
                    return true;
                item = parent;
            }

            return false;
        }
    }
}

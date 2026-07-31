using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    public sealed class RuntimeObjectRegistry
    {
        private sealed class Entry
        {
            public RuntimeObjectId Id;
            public Object Target;
        }

        private readonly Dictionary<int, Entry> _byInstance = new();
        private readonly Dictionary<long, Object> _byId = new();
        private readonly HashSet<int> _seen = new();
        private long _nextId = 1;

        public void BeginReconciliation() => _seen.Clear();

        public RuntimeObjectId GetOrCreate(Object target)
        {
            if (target == null)
                return default;
            int instanceId = target.GetInstanceID();
            _seen.Add(instanceId);
            if (_byInstance.TryGetValue(instanceId, out Entry entry) && ReferenceEquals(entry.Target, target))
                return entry.Id;
            var id = new RuntimeObjectId(_nextId++);
            _byInstance[instanceId] = new Entry { Id = id, Target = target };
            _byId[id.Value] = target;
            return id;
        }

        public void EndReconciliation()
        {
            var remove = new List<int>();
            foreach (KeyValuePair<int, Entry> pair in _byInstance)
                if (!_seen.Contains(pair.Key) || pair.Value.Target == null)
                    remove.Add(pair.Key);
            foreach (int instanceId in remove)
            {
                Entry entry = _byInstance[instanceId];
                _byInstance.Remove(instanceId);
                _byId.Remove(entry.Id.Value);
            }
        }

        public bool TryResolve<T>(RuntimeObjectId id, out T target) where T : Object
        {
            target = null;
            if (!id.IsValid || !_byId.TryGetValue(id.Value, out Object value) || value == null || value is not T typed)
                return false;
            target = typed;
            return true;
        }
    }
}

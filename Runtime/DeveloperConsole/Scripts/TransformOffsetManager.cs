using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities
{
    public sealed class TransformOffsetManager
    {
        private readonly Dictionary<Transform, Vector3> _originalPositions = new();

        public int TrackedCount => _originalPositions.Count;

        /// <summary>
        /// Applies an offset to the given transforms.
        /// Automatically tracks original positions and
        /// removes destroyed objects.
        /// </summary>
        public void ApplyOffset(IEnumerable<Transform> transforms, Vector3 offset)
        {
            CleanupDestroyed();

            foreach (var t in transforms)
            {
                if (!t) continue;

                // Backup original position only once
                if (!_originalPositions.ContainsKey(t))
                {
                    _originalPositions[t] = t.position;
                }

                t.position += offset;
            }
        }

        /// <summary>
        /// Restores all tracked transforms to original positions.
        /// Destroyed transforms are skipped and removed.
        /// </summary>
        public void Reset()
        {
            foreach (var kvp in _originalPositions)
            {
                if (!kvp.Key)
                    continue;

                kvp.Key.position = kvp.Value;
            }

            _originalPositions.Clear();
        }

        /// <summary>
        /// Restores a single transform and removes it from tracking.
        /// </summary>
        public void Reset(Transform t)
        {
            if (!t) return;

            if (_originalPositions.TryGetValue(t, out var pos))
            {
                t.position = pos;
                _originalPositions.Remove(t);
            }
        }

        /// <summary>
        /// Removes destroyed transforms from tracking.
        /// </summary>
        private void CleanupDestroyed()
        {
            if (_originalPositions.Count == 0)
                return;

            // Avoid modifying dictionary while iterating
            List<Transform> toRemove = null;

            foreach (var kvp in _originalPositions)
            {
                if (!kvp.Key)
                {
                    toRemove ??= new List<Transform>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                foreach (var t in toRemove)
                    _originalPositions.Remove(t);
            }
        }
    }
}

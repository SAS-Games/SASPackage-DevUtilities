using System;
using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Reusable component base for mini-tool collectors that publish immutable
    /// snapshots to a local controller and expose the latest snapshot to other
    /// consumers.
    /// </summary>
    public abstract class MiniToolSnapshotProviderBehaviour<TSnapshot> : MonoBehaviour, IMiniToolSnapshotProvider<TSnapshot> where TSnapshot : IMiniToolSnapshot
    {
        private TSnapshot _currentSnapshot;
        private bool _hasCurrentSnapshot;

        public event Action<TSnapshot> SnapshotChanged;

        public bool TryGetSnapshot(out TSnapshot snapshot)
        {
            snapshot = _currentSnapshot;
            return _hasCurrentSnapshot;
        }

        protected void PublishSnapshot(in TSnapshot snapshot)
        {
            _currentSnapshot = snapshot;
            _hasCurrentSnapshot = true;
            SnapshotChanged?.Invoke(snapshot);
        }

        protected void ClearSnapshot()
        {
            _currentSnapshot = default;
            _hasCurrentSnapshot = false;
        }
    }
}

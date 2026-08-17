using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Publishes the snapshot requested by the GraphicsInfo command. The latest
    /// requested snapshot remains available while the Player presentation is
    /// suppressed, allowing the remote provider to send the same state to the
    /// Debug Host.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dev Utilities/GraphicInfo/Provider")]
    public sealed class GraphicsInfoSnapshotProvider : MiniToolSnapshotProviderBehaviour<GraphicsInfoSnapshot>
    {
        private static GraphicsInfoSnapshotProvider _requestedProvider;
        private bool _requestedVisible;

        private void OnEnable()
        {
            if (!TryGetSnapshot(out _))
                Refresh(false);
        }

        private void OnDestroy()
        {
            if (_requestedProvider == this)
                _requestedProvider = null;
        }

        public void SetRequestedState(bool visible, bool verbose)
        {
            _requestedVisible = visible;
            if (!visible)
            {
                if (_requestedProvider == this)
                    _requestedProvider = null;
                return;
            }

            _requestedProvider = this;
            Refresh(verbose);
        }

        public void Refresh(bool verbose)
        {
            GraphicsInfoSnapshot snapshot = GraphicsInfoSnapshotCollector.Capture(verbose);
            PublishSnapshot(in snapshot);
        }

        public static bool TryGetRequestedSnapshot(out GraphicsInfoSnapshot snapshot)
        {
            GraphicsInfoSnapshotProvider provider = _requestedProvider;
            if (provider != null && provider._requestedVisible && provider.TryGetSnapshot(out snapshot))
                return true;

            snapshot = default;
            return false;
        }
    }
}

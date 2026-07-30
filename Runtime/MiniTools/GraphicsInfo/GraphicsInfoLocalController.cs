using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Connects the local GraphicsInfo snapshot provider to its view. The
    /// Editor Debug Host disables this controller and applies remote snapshots
    /// directly to the same view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(GraphicsInfoSnapshotProvider),
        typeof(global::GraphicsInfo))]
    public sealed class GraphicsInfoLocalController :
        MonoBehaviour,
        IMiniToolLocalController
    {
        [SerializeField]
        private GraphicsInfoSnapshotProvider m_SnapshotProvider;
        [SerializeField]
        private global::GraphicsInfo m_View;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            if (m_SnapshotProvider == null || m_View == null)
                return;

            m_SnapshotProvider.SnapshotChanged += ApplySnapshot;
            if (m_SnapshotProvider.TryGetSnapshot(
                    out GraphicsInfoSnapshot snapshot))
            {
                ApplySnapshot(snapshot);
            }
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
        }

        private void ApplySnapshot(GraphicsInfoSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
            {
                m_SnapshotProvider =
                    GetComponent<GraphicsInfoSnapshotProvider>();
            }

            if (m_View == null)
                m_View = GetComponent<global::GraphicsInfo>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}

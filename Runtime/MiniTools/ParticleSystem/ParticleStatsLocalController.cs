using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Connects the local particle snapshot provider to the shared view. The
    /// Debug Host applies remote snapshots directly to that same view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleStatsSnapshotProvider), typeof(ParticleStats))]
    [AddComponentMenu("Dev Utilities/ParticleStats/Controller")]
    public sealed class ParticleStatsLocalController : MonoBehaviour, IMiniToolLocalController
    {
        [SerializeField] private ParticleStatsSnapshotProvider m_SnapshotProvider;
        [SerializeField] private ParticleStats m_View;

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
            if (m_SnapshotProvider.TryGetSnapshot(out ParticleStatsSnapshot snapshot))
                ApplySnapshot(snapshot);
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
        }

        private void ApplySnapshot(ParticleStatsSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
                m_SnapshotProvider = GetComponent<ParticleStatsSnapshotProvider>();

            if (m_View == null)
                m_View = GetComponent<ParticleStats>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}

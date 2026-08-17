using UnityEngine;

namespace SAS.DevUtilities.Stats
{
    /// <summary>
    /// Connects the local Stats collector to the Stats view. The Editor Debug
    /// Host applies remote snapshot directly to the same view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StatsSnapshotProvider), typeof(Stats))]
    [AddComponentMenu("Dev Utilities/Stats/Controller")]
    public sealed class StatsLocalController : MonoBehaviour, IMiniToolLocalController
    {
        [SerializeField] private StatsSnapshotProvider m_SnapshotProvider;
        [SerializeField] private Stats m_View;

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
            if (m_SnapshotProvider.TryGetSnapshot(out StatsSnapshot snapshot))
                ApplySnapshot(snapshot);
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
        }

        private void ApplySnapshot(StatsSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
                m_SnapshotProvider = GetComponent<StatsSnapshotProvider>();
            if (m_View == null)
                m_View = GetComponent<Stats>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}

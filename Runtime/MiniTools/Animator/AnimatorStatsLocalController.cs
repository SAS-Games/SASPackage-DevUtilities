using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Connects the local Animator collector to the existing AnimatorStats
    /// view. The Debug Host bypasses this controller and applies remote
    /// snapshots directly to the same view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnimatorStatsSnapshotProvider), typeof(AnimatorStats))]
    [AddComponentMenu("Dev Utilities/AnimatorStats/Controller")]
    public sealed class AnimatorStatsLocalController : MonoBehaviour, IMiniToolLocalController
    {
        [SerializeField] private AnimatorStatsSnapshotProvider m_SnapshotProvider;
        [SerializeField] private AnimatorStats m_View;

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
            if (m_SnapshotProvider.TryGetSnapshot(out AnimatorStatsSnapshot snapshot))
                ApplySnapshot(snapshot);
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
        }

        private void ApplySnapshot(AnimatorStatsSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
                m_SnapshotProvider = GetComponent<AnimatorStatsSnapshotProvider>();

            if (m_View == null)
                m_View = GetComponent<AnimatorStats>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}

using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Connects the local FPS collector to the lightweight FPS view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FPSSnapshotProvider), typeof(FPS))]
    public sealed class FPSLocalController : MonoBehaviour, IMiniToolLocalController
    {
        [SerializeField] private FPSSnapshotProvider m_SnapshotProvider;
        [SerializeField] private FPS m_View;

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
            if (m_SnapshotProvider.TryGetSnapshot(out FPSSnapshot snapshot))
                ApplySnapshot(snapshot);
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
        }

        private void ApplySnapshot(FPSSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
                m_SnapshotProvider = GetComponent<FPSSnapshotProvider>();
            if (m_View == null)
                m_View = GetComponent<FPS>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}

using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Connects the local GameInfo collector to the GameInfo view. The Editor
    /// Debug Host bypasses this controller and applies remote snapshot directly to
    /// the same view.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameInfoSnapshotProvider), typeof(GameInfoComponent))]
    [AddComponentMenu("Dev Utilities/GameInfo/Controller")]
    public sealed class GameInfoLocalController : MonoBehaviour, IMiniToolLocalController
    {
        [SerializeField] private GameInfoSnapshotProvider m_SnapshotProvider;
        [SerializeField] private GameInfoComponent m_View;

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
            if (m_SnapshotProvider.TryGetSnapshot(out GameInfoSnapshot snapshot))
                ApplySnapshot(snapshot);
        }

        private void OnDisable()
        {
            if (m_SnapshotProvider != null)
                m_SnapshotProvider.SnapshotChanged -= ApplySnapshot;
        }

        private void ApplySnapshot(GameInfoSnapshot snapshot)
        {
            if (m_View != null)
                m_View.ApplySnapshot(in snapshot);
        }

        private void ResolveDependencies()
        {
            if (m_SnapshotProvider == null)
                m_SnapshotProvider = GetComponent<GameInfoSnapshotProvider>();
            if (m_View == null)
                m_View = GetComponent<GameInfoComponent>();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            ResolveDependencies();
        }
#endif
    }
}

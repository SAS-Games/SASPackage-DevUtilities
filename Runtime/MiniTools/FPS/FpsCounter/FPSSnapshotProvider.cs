using UnityEngine;

namespace HP.DevUtilities
{
    /// <summary>
    /// Publishes lightweight FPS snapshots for Players. It contains
    /// no UI rendering logic.
    /// </summary>
    [AddComponentMenu("Dev Utilities/FPS/SnapshotProvider")]
    public sealed class FPSSnapshotProvider : MiniToolSnapshotProviderBehaviour<FPSSnapshot>
    {
        private const float MinimumUpdateInterval = 0.05f;

        [SerializeField, Min(MinimumUpdateInterval)] private float m_UpdateInterval = 0.5f;

        [Tooltip("Fallback target frame rate used when Application.targetFrameRate " + "is not greater than zero.")]
        [SerializeField, Min(1)]
        private int m_TargetFrameRate = FPSSnapshotCollector.DefaultFallbackTargetFrameRate;

        private PerformanceSampleCursor _sampleCursor;

        private void Awake()
        {
            enabled = !PerformanceOverlaySelection.UseDetailedStats;
        }

        private void OnEnable()
        {
            ResetSample();
            ClearSnapshot();
        }

        private void Update()
        {
#if !ENABLE_DEBUG
            return;
#else
            PerformanceSnapshotSource.Tick(false);

            double updateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
            if (PerformanceSnapshotSource.GetElapsedSeconds(in _sampleCursor) < updateInterval)
                return;

            Refresh();
#endif
        }

        public void Refresh()
        {
            if (!PerformanceSnapshotSource.TryConsume(ref _sampleCursor, out double elapsedSeconds, out int frames) || !FPSSnapshotCollector.TryCapture(elapsedSeconds, frames, m_TargetFrameRate, out FPSSnapshot snapshot))
                return;

            PublishSnapshot(in snapshot);
        }

        private void ResetSample()
        {
            _sampleCursor = PerformanceSnapshotSource.CreateCursor();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_UpdateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
            m_TargetFrameRate = Mathf.Max(1, m_TargetFrameRate);
        }
#endif
    }
}

using UnityEngine;

namespace SAS.DevUtilities.Stats
{
    /// <summary>
    /// Samples performance data and publishes Stats snapshots for the local Player
    /// prefab. It contains no UI rendering logic.
    /// </summary>
    [AddComponentMenu("Dev Utilities/Stats/SnapshotProvider")]
    public sealed class StatsSnapshotProvider : MiniToolSnapshotProviderBehaviour<StatsSnapshot>
    {
        private const float MinimumUpdateInterval = 0.05f;

        [SerializeField, Min(MinimumUpdateInterval)] private float m_UpdateInterval = 0.5f;

        private PerformanceSampleCursor _sampleCursor;

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

        private void Awake()
        {
#if UNITY_EDITOR
            FPS fps = GetComponent<FPS>();
            if (fps != null)
                fps.enabled = false;
            enabled = true;
#else
            enabled = UnityEngine.Debug.isDebugBuild;
#endif
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
            PerformanceSnapshotSource.Tick(true);

            double updateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
            if (PerformanceSnapshotSource.GetElapsedSeconds(in _sampleCursor) < updateInterval)
                return;

            Refresh();
#endif
        }

        public void Refresh()
        {
            if (!PerformanceSnapshotSource.TryConsume(ref _sampleCursor, out double elapsedSeconds, out int frames) || !StatsSnapshotCollector.TryCapture(elapsedSeconds, frames, _frameTimings, out StatsSnapshot snapshot))
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
        }
#endif
    }
}

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

        private double _elapsedSeconds;
        private int _frames;

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
            float deltaTime = Time.unscaledDeltaTime;
            if (!IsValidDeltaTime(deltaTime))
                return;

            FrameTimingManager.CaptureFrameTimings();
            _elapsedSeconds += deltaTime;
            _frames++;

            double updateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
            if (_elapsedSeconds < updateInterval)
                return;

            Refresh();
            ResetSample();
#endif
        }

        public void Refresh()
        {
            if (!StatsSnapshotCollector.TryCapture(_elapsedSeconds, _frames, _frameTimings, out StatsSnapshot snapshot))
                return;

            PublishSnapshot(in snapshot);
        }

        private static bool IsValidDeltaTime(float deltaTime)
        {
            return deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime);
        }

        private void ResetSample()
        {
            _elapsedSeconds = 0d;
            _frames = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_UpdateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
        }
#endif
    }
}

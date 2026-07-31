using UnityEngine;

namespace SAS.DevUtilities
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

        private double _elapsedSeconds;
        private int _frames;

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
            float deltaTime = Time.unscaledDeltaTime;
            if (!IsValidDeltaTime(deltaTime))
                return;

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
            if (!FPSSnapshotCollector.TryCapture(_elapsedSeconds, _frames, m_TargetFrameRate, out FPSSnapshot snapshot))
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
            m_TargetFrameRate = Mathf.Max(1, m_TargetFrameRate);
        }
#endif
    }
}

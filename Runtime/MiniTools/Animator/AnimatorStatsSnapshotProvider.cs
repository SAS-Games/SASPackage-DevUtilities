using Unity.Profiling;
using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Periodically publishes Animator statistics for the local Player prefab.
    /// It contains no UI rendering logic.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Dev Utilities/AnimatorStats/SnapshotProvider")]
    public sealed class AnimatorStatsSnapshotProvider : MiniToolSnapshotProviderBehaviour<AnimatorStatsSnapshot>
    {
        private const float MinimumUpdateInterval = 0.05f;

        [SerializeField, Min(MinimumUpdateInterval)] private float m_UpdateInterval = 1f;

        private float _elapsed;
        private ProfilerRecorder _animationUpdateRecorder;

        private void OnEnable()
        {
            _elapsed = 0f;
            StartRecorder();
            Refresh();
        }

        private void OnDisable()
        {
            DisposeRecorder();
            ClearSnapshot();
        }

        private void Update()
        {
#if !ENABLE_DEBUG
            return;
#else
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < m_UpdateInterval)
                return;

            _elapsed = 0f;
            Refresh();
#endif
        }

        public void Refresh()
        {
            AnimatorStatsSnapshot snapshot = AnimatorStatsSnapshotCollector.Capture(in _animationUpdateRecorder);
            PublishSnapshot(in snapshot);
        }

        private void StartRecorder()
        {
            DisposeRecorder();
            _animationUpdateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Animation, "Animators.Update");
        }

        private void DisposeRecorder()
        {
            if (_animationUpdateRecorder.Valid)
                _animationUpdateRecorder.Dispose();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_UpdateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
        }
#endif
    }
}

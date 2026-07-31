using System;
using Unity.Profiling;
using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Recoverable Animator statistics shared by the Player overlay and the
    /// Editor Debug Host.
    /// </summary>
    [Serializable]
    public struct AnimatorStatsSnapshot : IMiniToolSnapshot
    {
        public int Total;
        public int Initialized;
        public int ActiveAlways;
        public int ActiveCullUpdate;
        public int ActiveCullCompletely;
        public int DisabledAlways;
        public int DisabledCullUpdate;
        public int DisabledCullCompletely;
        public bool HasCpuTiming;
        public double CpuTimeMs;
    }

    /// <summary>
    /// Collects Animator state without containing presentation or transport
    /// responsibilities.
    /// </summary>
    public static class AnimatorStatsSnapshotCollector
    {
        public static AnimatorStatsSnapshot Capture(
            in ProfilerRecorder animationUpdateRecorder)
        {
            Animator[] animators =
                UnityEngine.Object.FindObjectsByType<Animator>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var snapshot = new AnimatorStatsSnapshot
            {
                Total = animators.Length,
                HasCpuTiming =
                    animationUpdateRecorder.Valid &&
                    animationUpdateRecorder.IsRunning
            };

            foreach (Animator animator in animators)
            {
                if (animator == null)
                    continue;

                if (animator.isInitialized)
                    snapshot.Initialized++;

                bool active =
                    animator.enabled &&
                    animator.gameObject.activeInHierarchy;
                switch (animator.cullingMode)
                {
                    case AnimatorCullingMode.AlwaysAnimate:
                        if (active)
                            snapshot.ActiveAlways++;
                        else
                            snapshot.DisabledAlways++;
                        break;
                    case AnimatorCullingMode.CullUpdateTransforms:
                        if (active)
                            snapshot.ActiveCullUpdate++;
                        else
                            snapshot.DisabledCullUpdate++;
                        break;
                    case AnimatorCullingMode.CullCompletely:
                        if (active)
                            snapshot.ActiveCullCompletely++;
                        else
                            snapshot.DisabledCullCompletely++;
                        break;
                }
            }

            if (snapshot.HasCpuTiming)
            {
                snapshot.CpuTimeMs =
                    animationUpdateRecorder.LastValue * 1e-6d;
            }

            return snapshot;
        }
    }
}

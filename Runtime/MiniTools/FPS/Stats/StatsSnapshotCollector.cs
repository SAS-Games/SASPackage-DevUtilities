using UnityEngine;
using UnityEngine.Profiling;

namespace SAS.DevUtilities.Stats
{
    /// <summary>
    /// Collects the canonical Stats snapshot used by local and remote providers.
    /// </summary>
    internal static class StatsSnapshotCollector
    {
        internal static bool TryCapture(double elapsedSeconds, int frames, FrameTiming[] frameTimings, out StatsSnapshot snapshot)
        {
            if (frames <= 0 || elapsedSeconds <= 0d)
            {
                snapshot = default;
                return false;
            }

            snapshot = new StatsSnapshot
            {
                AverageFps = frames / elapsedSeconds,
                AverageFrameTimeMs = elapsedSeconds * 1000d / frames,
                TargetFrameRate = Application.targetFrameRate,
                VSyncCount = QualitySettings.vSyncCount,
                AllocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(),
                ReservedMemoryBytes = Profiler.GetTotalReservedMemoryLong(),
                UnusedReservedMemoryBytes = Profiler.GetTotalUnusedReservedMemoryLong()
            };

            PopulateFrameTiming(frameTimings, ref snapshot);
            return true;
        }

        private static void PopulateFrameTiming(FrameTiming[] frameTimings, ref StatsSnapshot snapshot)
        {
            if (frameTimings == null || frameTimings.Length == 0 || FrameTimingManager.GetLatestTimings(1, frameTimings) <= 0)
            {
                return;
            }

            FrameTiming timing = frameTimings[0];
            snapshot.HasFrameTiming = true;
            snapshot.CpuFrameTimeMs = timing.cpuFrameTime;
            snapshot.CpuMainThreadFrameTimeMs = timing.cpuMainThreadFrameTime;
            snapshot.CpuRenderThreadFrameTimeMs = timing.cpuRenderThreadFrameTime;
            snapshot.CpuPresentWaitTimeMs = timing.cpuMainThreadPresentWaitTime;
            snapshot.GpuFrameTimeMs = timing.gpuFrameTime;
        }
    }
}

using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Collects the canonical FPS snapshot used by local providers.
    /// </summary>
    internal static class FPSSnapshotCollector
    {
        internal const int DefaultFallbackTargetFrameRate = 60;

        internal static bool TryCapture(
            double elapsedSeconds,
            int frames,
            int fallbackTargetFrameRate,
            out FPSSnapshot snapshot)
        {
            if (frames <= 0 || elapsedSeconds <= 0d)
            {
                snapshot = default;
                return false;
            }

            double averageFps =
                frames / elapsedSeconds;
            double averageFrameTimeMs =
                elapsedSeconds * 1000d / frames;
            int targetFrameRate =
                Application.targetFrameRate > 0
                    ? Application.targetFrameRate
                    : Mathf.Max(1, fallbackTargetFrameRate);
            double targetFrameTimeMs =
                1000d / targetFrameRate;

            snapshot = new FPSSnapshot
            {
                AverageFps = averageFps,
                AverageFrameTimeMs = averageFrameTimeMs,
                TargetFrameRate = targetFrameRate,
                TargetFrameTimeMs = targetFrameTimeMs,
                IsFrameTimeOverBudget =
                    averageFrameTimeMs > targetFrameTimeMs
            };
            return true;
        }
    }
}

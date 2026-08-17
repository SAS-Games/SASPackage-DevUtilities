using UnityEngine;

namespace HP.DevUtilities
{
    internal struct PerformanceSampleCursor
    {
        internal double ElapsedSeconds;
        internal long FrameCount;
    }

    /// <summary>
    /// Shares the per-frame performance sample between local overlays and remote
    /// providers. Each consumer keeps an independent cursor, so different update
    /// intervals do not duplicate frame timing capture or interfere with one another.
    /// </summary>
    internal static class PerformanceSnapshotSource
    {
        private static double s_TotalElapsedSeconds;
        private static long s_TotalFrames;
        private static int s_LastSampledFrame = -1;
        private static int s_LastFrameTimingCapture = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_TotalElapsedSeconds = 0d;
            s_TotalFrames = 0L;
            s_LastSampledFrame = -1;
            s_LastFrameTimingCapture = -1;
        }

        internal static PerformanceSampleCursor CreateCursor()
        {
            return new PerformanceSampleCursor
            {
                ElapsedSeconds = s_TotalElapsedSeconds,
                FrameCount = s_TotalFrames
            };
        }

        internal static void Tick(bool captureDetailedFrameTiming)
        {
#if !ENABLE_DEBUG
            return;
#else
            int frame = Time.frameCount;
            if (frame != s_LastSampledFrame)
            {
                if (s_LastSampledFrame >= 0 && frame < s_LastSampledFrame)
                    Reset();

                float deltaTime = Time.unscaledDeltaTime;
                if (IsValidDeltaTime(deltaTime))
                {
                    s_TotalElapsedSeconds += deltaTime;
                    s_TotalFrames++;
                }

                s_LastSampledFrame = frame;
            }

            if (captureDetailedFrameTiming && frame != s_LastFrameTimingCapture)
            {
                FrameTimingManager.CaptureFrameTimings();
                s_LastFrameTimingCapture = frame;
            }
#endif
        }

        internal static double GetElapsedSeconds(in PerformanceSampleCursor cursor)
        {
            double elapsedSeconds = s_TotalElapsedSeconds - cursor.ElapsedSeconds;
            return elapsedSeconds > 0d ? elapsedSeconds : 0d;
        }

        internal static bool TryConsume(ref PerformanceSampleCursor cursor, out double elapsedSeconds, out int frames)
        {
            elapsedSeconds = s_TotalElapsedSeconds - cursor.ElapsedSeconds;
            long sampledFrames = s_TotalFrames - cursor.FrameCount;
            cursor = CreateCursor();
            if (elapsedSeconds <= 0d || sampledFrames <= 0L)
            {
                elapsedSeconds = 0d;
                frames = 0;
                return false;
            }

            frames = sampledFrames > int.MaxValue ? int.MaxValue : (int)sampledFrames;
            return true;
        }

        private static bool IsValidDeltaTime(float deltaTime)
        {
            return deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime);
        }
    }
}

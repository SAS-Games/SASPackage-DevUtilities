using System;

namespace HP.DevUtilities.Stats
{
    [Serializable]
    public struct StatsSnapshot : IMiniToolSnapshot
    {
        public double AverageFps;
        public double AverageFrameTimeMs;

        public int TargetFrameRate;
        public int VSyncCount;

        public bool HasFrameTiming;
        public double CpuFrameTimeMs;
        public double CpuMainThreadFrameTimeMs;
        public double CpuRenderThreadFrameTimeMs;
        public double CpuPresentWaitTimeMs;
        public double GpuFrameTimeMs;

        public long AllocatedMemoryBytes;
        public long ReservedMemoryBytes;
        public long UnusedReservedMemoryBytes;
    }
}

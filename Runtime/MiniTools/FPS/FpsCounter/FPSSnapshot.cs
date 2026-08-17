using System;

namespace HP.DevUtilities
{
    /// <summary>
    /// Recoverable snapshot displayed by the lightweight FPS mini-tool.
    /// </summary>
    [Serializable]
    public struct FPSSnapshot : IMiniToolSnapshot
    {
        public double AverageFps;
        public double AverageFrameTimeMs;

        public int TargetFrameRate;
        public double TargetFrameTimeMs;

        public bool IsFrameTimeOverBudget;
    }
}

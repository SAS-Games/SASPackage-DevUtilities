using UnityEngine;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Captures FrameStepper data without owning presentation or input.
    /// </summary>
    internal static class FrameStepperSnapshotCollector
    {
        internal static FrameStepperSnapshot Capture()
        {
            float timeScale = Time.timeScale;
            return new FrameStepperSnapshot
            {
                IsPaused = timeScale <= 0f,
                TimeScale = timeScale
            };
        }
    }
}

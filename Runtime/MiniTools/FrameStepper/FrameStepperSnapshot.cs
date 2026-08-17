using System;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Recoverable FrameStepper state shared by the local Player and remote
    /// Debug Host presentations.
    /// </summary>
    [Serializable]
    public struct FrameStepperSnapshot : IMiniToolSnapshot
    {
        public bool IsPaused;
        public float TimeScale;
    }
}

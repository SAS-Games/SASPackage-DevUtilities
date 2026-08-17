namespace HP.DevUtilities
{
    /// <summary>
    /// Keeps the original mutually exclusive Stats/FPS selection in one place.
    /// </summary>
    internal static class PerformanceOverlaySelection
    {
        internal static bool UseDetailedStats
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return UnityEngine.Debug.isDebugBuild;
#endif
            }
        }
    }
}

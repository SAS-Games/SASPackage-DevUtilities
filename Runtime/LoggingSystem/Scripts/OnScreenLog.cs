
namespace SAS
{
    public static partial class Debug
    {
        private static OnScreenLogUI onScreenLogger;

        public static void InitializeOnScreenLogger(OnScreenLogUI logger)
        {
            onScreenLogger = logger;
        }

        private static partial void AddOnScreenLogEntry(string message, string tag, LogLevel level)
        {
            onScreenLogger?.AddLog(message, level, tag);
        }
    }
}

// OnScreenLogUI.cs remains mostly the same, just needs to make AddLog public
// and remove hard dependencies on UnityEngine.Debug

// Update to OnScreenLogUI.cs
// Inside OnScreenLogUI.cs namespace:

// public void AddLog(string message, LogLevel level, string tag = "")
// is already public and well defined
// Ensure you call Debug.InitializeOnScreenLogger(this); in OnScreenLogUI.Awake or elsewhere

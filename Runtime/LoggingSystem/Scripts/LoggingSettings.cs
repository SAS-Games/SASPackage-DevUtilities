using UnityEngine;

namespace SAS
{
    public static class DebugSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyDefaults()
        {
            var config = Resources.Load<LoggingConfig>("DebugSettings");
            if (config != null)
            {
                Debug.SetLogLevel((int)config.logLevel);
                Debug.SetAllowedTags(config.allowedTags);
                UnityEngine.Debug.Log($"[DebugSettings] Applied project debug config (LogLevel={config.logLevel}).");
            }
        }
    }
}
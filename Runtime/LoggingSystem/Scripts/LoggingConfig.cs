using System.Collections.Generic;
using UnityEngine;

namespace SAS
{
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "HP/Debug/Settings")]
    public class LoggingConfig : ScriptableObject
    {
        [Tooltip("Enable or disable specific log levels.")]
        public LogLevel logLevel = LogLevel.Info | LogLevel.Warning | LogLevel.Error;

        [Tooltip("Only logs with these tags will be shown. Leave empty to show all.")]
        public List<string> allowedTags = new();
    }
}
using System.Collections.Generic;
using UnityEngine;

namespace SAS
{
<<<<<<< HEAD
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "HP/Debug/Settings")]
=======
    [CreateAssetMenu(fileName = "DebugSettings", menuName = "SAS/Debug/Settings")]
>>>>>>> refs/remotes/origin/master
    public class LoggingConfig : ScriptableObject
    {
        [Tooltip("Enable or disable specific log levels.")]
        public LogLevel logLevel = LogLevel.Info | LogLevel.Warning | LogLevel.Error;

        [Tooltip("Only logs with these tags will be shown. Leave empty to show all.")]
        public List<string> allowedTags = new();
    }
}
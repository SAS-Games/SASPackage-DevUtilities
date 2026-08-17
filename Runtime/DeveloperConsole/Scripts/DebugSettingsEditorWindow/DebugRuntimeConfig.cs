using System.Collections.Generic;
using HP;
using UnityEngine;

public class DebugRuntimeConfig : ScriptableObject
{
    public bool pauseOnEnable;
    public LogLevel logLevel;
    public List<string> allowedTags;
}
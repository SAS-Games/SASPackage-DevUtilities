//using System;
//using System.Collections.Generic;
//using UnityEngine;

//namespace SAS
//{
//    public static partial class Debug
//    {
//        private static bool showOnScreenLog = false;
//        private static Vector2 scrollPosition = Vector2.zero;
//        private static readonly List<LogEntry> logEntries = new List<LogEntry>();
//        private static int maxLogEntries = 50;
//        private static float logEntryLifetime = 0f; // 0 = infinite lifetime
//        private static GUIStyle logEntryStyle;
//        private static Texture2D backgroundTexture;
//        private static Rect logWindowRect = new Rect(10, 10, 600, 300);
//        private static bool initializedStyles = false;

//        private class LogEntry
//        {
//            public string Message;
//            public LogLevel Level;
//            public string Tag;
//            public DateTime Timestamp;
//            public float CreationTime;
//        }

//        public static void ToggleOnScreenLog()
//        {
//            showOnScreenLog = !showOnScreenLog;
//        }

//        public static void SetMaxLogEntries(int max)
//        {
//            maxLogEntries = Mathf.Max(10, max);
//        }

//        /// <summary>
//        /// Set the lifetime duration for log entries in seconds (0 = infinite)
//        /// </summary>
//        public static void SetLogEntryLifetime(float seconds)
//        {
//            logEntryLifetime = Mathf.Max(0, seconds);
//        }

//        // private static partial void AddOnScreenLogEntry(string message, string tag, LogLevel level)
//        // {
//        //     if(logEntries.Count > maxLogEntries)
//        //         logEntries.RemoveAt(0);
//        //
//        //     // Create and add new entry
//        //     logEntries.Add(new LogEntry
//        //     {
//        //         Message = message,
//        //         Level = level,
//        //         Tag = tag,
//        //         Timestamp = DateTime.Now,
//        //         CreationTime = Time.realtimeSinceStartup
//        //     });
//        //
//        //     // Maintain max log entries
//        //     while (logEntries.Count > maxLogEntries)
//        //     {
//        //         logEntries.RemoveAt(0);
//        //     }
//        //
//        //     // Auto-scroll to bottom
//        //     scrollPosition.y = float.MaxValue;
//        // }

//        private static void CleanupExpiredEntries()
//        {
//            if (logEntryLifetime <= 0) return; // Infinite lifetime

//            float currentTime = Time.realtimeSinceStartup;
//            for (int i = logEntries.Count - 1; i >= 0; i--)
//            {
//                float entryAge = currentTime - logEntries[i].CreationTime;
//                if (entryAge > logEntryLifetime)
//                {
//                    logEntries.RemoveAt(i);
//                }
//            }
//        }

//        public static void DrawOnScreenLog()
//        {
//            if (!showOnScreenLog) return;

//            InitializeStyles();

//            logWindowRect = GUILayout.Window(0, logWindowRect, OnLogWindow, "Debug Console");
//            logWindowRect.x = Mathf.Clamp(logWindowRect.x, 0, Screen.width - logWindowRect.width);
//            logWindowRect.y = Mathf.Clamp(logWindowRect.y, 0, Screen.height - logWindowRect.height);
//        }

//        private static void InitializeStyles()
//        {
//            if (initializedStyles) return;

//            // Create background texture
//            backgroundTexture = new Texture2D(1, 1);
//            backgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.9f));
//            backgroundTexture.Apply();

//            // Log entry style
//            logEntryStyle = new GUIStyle(GUI.skin.label)
//            {
//                fontSize = 14,
//                wordWrap = true,
//                richText = true
//            };

//            initializedStyles = true;
//        }

//        private static void OnLogWindow(int windowID)
//        {
//            // Toolbar with controls
//            GUILayout.BeginHorizontal();
//            if (GUILayout.Button("Clear", GUILayout.Width(60)))
//            {
//                logEntries.Clear();
//            }
//            if (GUILayout.Button(showOnScreenLog ? "Hide" : "Show", GUILayout.Width(60)))
//            {
//                showOnScreenLog = !showOnScreenLog;
//            }

//            // Lifetime control
//            GUILayout.Label("Lifetime:", GUILayout.Width(60));
//            float newLifetime = GUILayout.HorizontalSlider(logEntryLifetime, 0, 60, GUILayout.Width(100));
//            if (Math.Abs(newLifetime - logEntryLifetime) > 0.01f)
//            {
//                SetLogEntryLifetime(newLifetime);
//            }
//            GUILayout.Label($"{logEntryLifetime:F1}s", GUILayout.Width(40));

//            GUILayout.EndHorizontal();

//            // Log entries display
//            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
//            for (int i = 0; i < logEntries.Count;)
//            {
//                LogEntry entry = logEntries[i];
//                string baseColorHex = entry.Level switch
//                {
//                    LogLevel.Warning => "FFFF00", // Yellow
//                    LogLevel.Error => "FF0000",   // Red
//                    _ => "FFFFFF"                 // White
//                };

//                string timestamp = entry.Timestamp.ToString("HH:mm:ss.fff");
//                string tagDisplay = string.IsNullOrEmpty(entry.Tag) ? "" : $"[{entry.Tag}] ";

//                // Calculate fade effect based on lifetime
//                float alpha = 1f;
//                if (logEntryLifetime > 0)
//                {
//                    float age = Time.realtimeSinceStartup - entry.CreationTime;
//                    if (age > logEntryLifetime * 0.7f) // Start fading in last 30% of lifetime
//                    {
//                        alpha = 1f - (age - logEntryLifetime * 0.7f) / (logEntryLifetime * 0.3f);
//                        if (alpha <= 0f)
//                        {
//                            logEntries.RemoveAt(i);
//                            continue;
//                        }
//                    }
//                }

//                // Apply alpha to color
//                string alphaHex = Mathf.FloorToInt(alpha * 255).ToString("X2");
//                string colorWithAlpha = $"{baseColorHex}{alphaHex}";

//                GUILayout.Label($"<color=#{colorWithAlpha}>[{timestamp}] {tagDisplay}{entry.Message}</color>", logEntryStyle);
//                ++i;
//            }
//            GUILayout.EndScrollView();

//            // Display log count and lifetime info
//            GUILayout.BeginHorizontal();
//            GUILayout.Label($"Entries: {logEntries.Count}/{maxLogEntries}");
//            GUILayout.FlexibleSpace();
//            GUILayout.Label(logEntryLifetime > 0 ? $"Auto-remove: {logEntryLifetime:F1}s" : "Auto-remove: Off");
//            GUILayout.EndHorizontal();

//            // Allow dragging the window
//            GUI.DragWindow(new Rect(0, 0, 10000, 20));
//        }
//    }
//}

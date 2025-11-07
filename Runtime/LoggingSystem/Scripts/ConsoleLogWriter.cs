using System;
using UnityEngine;
#if LOG_TO_FILE
using System.IO;
#endif

/// <summary>
/// Captures all Debug.Log/Warning/Error/Exception messages.
/// Always prints logs to Console.
/// Optional file output controlled by: LOG_TO_FILE.
/// </summary>
#if ENABLE_LOG_CATCHER
public static class ConsoleLogWriter
{
    private static bool _initialized;

#if LOG_TO_FILE
    private static string _logFilePath;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

#if LOG_TO_FILE
        try
        {
            _logFilePath = Path.Combine(Application.persistentDataPath, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
            File.WriteAllText(_logFilePath, $"=== Log Session Started at {DateTime.Now} ===\n\n");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ConsoleLogWriter] Failed to create log file: {e.Message}");
        }
#endif

        Application.logMessageReceived += HandleLog;
        Console.WriteLine("[ConsoleLogWriter] Initialized.");
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        Application.logMessageReceived -= HandleLog;
        _initialized = false;

        Console.WriteLine("[ConsoleLogWriter] Shutdown.");
    }

    private static void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Always print to console
        Console.WriteLine($"[{type}] {logString}\n{stackTrace}");

#if LOG_TO_FILE
        try
        {
            File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss}] [{type}] {logString}\n{stackTrace}\n");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ConsoleLogWriter] File write error: {e.Message}");
        }
#endif
    }
}
#endif
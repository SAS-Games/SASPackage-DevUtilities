using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;

/// <summary>
/// Displays interval FPS, frame timing, target frame rate, VSync, and memory statistics.
/// The FPS value is calculated from unscaled elapsed time so it remains valid while the
/// game is paused and is not biased by averaging reciprocal frame times.
/// </summary>
public class Stats : UIBehaviour
{
    private const float MinimumUpdateInterval = 0.05f;
    private const double BytesPerGibibyte = 1073741824d;

    [Header("Display")]
    [SerializeField] private Text m_Display = default;
    [Min(MinimumUpdateInterval)]
    [SerializeField] private float m_UpdateInterval = 0.5f;

    private double _elapsedSeconds;
    private int _frames;

    private readonly StringBuilder _builder = new StringBuilder(512);
    private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

    protected override void Awake()
    {
        base.Awake();

#if UNITY_EDITOR
        var fps = GetComponent<FPS>();
        if (fps != null)
            fps.enabled = false;
        enabled = true;
#else
        enabled = Debug.isDebugBuild;
#endif
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetSample();
    }

    private void Update()
    {
#if !ENABLE_DEBUG
        return;
#else
        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            return;

        FrameTimingManager.CaptureFrameTimings();

        _elapsedSeconds += deltaTime;
        _frames++;

        double updateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
        if (_elapsedSeconds < updateInterval)
            return;

        double averageFps = _frames / _elapsedSeconds;
        double averageFrameTimeMs = _elapsedSeconds * 1000d / _frames;

        RefreshDisplay(averageFps, averageFrameTimeMs);
        ResetSample();
#endif
    }

    private void RefreshDisplay(double averageFps, double averageFrameTimeMs)
    {
        Color fpsColor = averageFps < 30d
            ? (averageFps < 10d ? Color.red : Color.yellow)
            : Color.green;

        _builder.Length = 0;
        _builder.Append("<color=#")
            .Append(ColorUtility.ToHtmlStringRGB(fpsColor))
            .Append(">FPS: ")
            .Append(averageFps.ToString("F1"))
            .Append("</color>\n");
        _builder.AppendFormat("Average Frame Time: {0:F2} ms\n", averageFrameTimeMs);

        _builder.Append("Target FPS: ");
        if (Application.targetFrameRate < 0)
            _builder.Append("Platform Default (-1)");
        else
            _builder.Append(Application.targetFrameRate);

        _builder.AppendFormat("\nVSync Count: {0}\n", QualitySettings.vSyncCount);

        if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
        {
            FrameTiming timing = _frameTimings[0];
            _builder.AppendFormat("Latest CPU Frame: {0:F3} ms\n", timing.cpuFrameTime);
            _builder.AppendFormat("Latest CPU Main Thread: {0:F3} ms\n", timing.cpuMainThreadFrameTime);
            _builder.AppendFormat("Latest CPU Render Thread: {0:F3} ms\n", timing.cpuRenderThreadFrameTime);
            _builder.AppendFormat("Latest CPU Present Wait: {0:F3} ms\n", timing.cpuMainThreadPresentWaitTime);
            _builder.AppendFormat("Latest GPU Frame: {0:F3} ms\n", timing.gpuFrameTime);
        }
        else
        {
            _builder.Append("Detailed Frame Timing: unavailable\n");
        }

        _builder.AppendFormat("Allocated: {0:F3} GiB\n", Profiler.GetTotalAllocatedMemoryLong() / BytesPerGibibyte);
        _builder.AppendFormat("Reserved: {0:F3} GiB\n", Profiler.GetTotalReservedMemoryLong() / BytesPerGibibyte);
        _builder.AppendFormat("Unused: {0:F3} GiB", Profiler.GetTotalUnusedReservedMemoryLong() / BytesPerGibibyte);

        m_Display.text = _builder.ToString();
    }

    private void ResetSample()
    {
        _elapsedSeconds = 0d;
        _frames = 0;
    }
}

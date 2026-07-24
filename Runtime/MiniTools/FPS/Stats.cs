using System.Text;
using Unity.Profiling;
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
    private ProfilerRecorder _batches;
    private ProfilerRecorder _setPassCalls;
    private ProfilerRecorder _drawCalls;
    private ProfilerRecorder _triangles;
    private ProfilerRecorder _vertices;
    private ProfilerRecorder _shadowCasters;
    private ProfilerRecorder _renderTextureCount;
    private ProfilerRecorder _renderTextureMemory;

    protected override void Awake()
    {
        base.Awake();

#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_DEBUG
        var fps = GetComponent<FPS>();
        if (fps != null)
            fps.enabled = false;
        enabled = true;
#else
        enabled = false;
#endif
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        StartRenderingRecorders();
        ResetSample();
    }

    protected override void OnDisable()
    {
        DisposeRenderingRecorders();
        base.OnDisable();
    }

    private void Update()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && !ENABLE_DEBUG
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

        AppendCounter("Batches", _batches);
        AppendCounter("SetPass Calls", _setPassCalls);
        AppendCounter("Draw Calls", _drawCalls);
        AppendCounter("Triangles", _triangles);
        AppendCounter("Vertices", _vertices);
        AppendCounter("Shadow Casters", _shadowCasters);
        AppendCounter("Render Textures", _renderTextureCount);
        AppendMemoryCounter("Render Texture Memory", _renderTextureMemory);

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

    private void StartRenderingRecorders()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_DEBUG
        DisposeRenderingRecorders();
        _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _setPassCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        _shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
        _renderTextureCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Render Texture Count");
        _renderTextureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Render Texture Memory");
#endif
    }

    private void DisposeRenderingRecorders()
    {
        _batches.Dispose();
        _setPassCalls.Dispose();
        _drawCalls.Dispose();
        _triangles.Dispose();
        _vertices.Dispose();
        _shadowCasters.Dispose();
        _renderTextureCount.Dispose();
        _renderTextureMemory.Dispose();
    }

    private void AppendCounter(string label, ProfilerRecorder recorder)
    {
        if (recorder.Valid)
            _builder.Append(label).Append(": ").Append(recorder.LastValue).Append('\n');
    }

    private void AppendMemoryCounter(string label, ProfilerRecorder recorder)
    {
        if (recorder.Valid)
            _builder.Append(label).Append(": ").Append((recorder.LastValue / BytesPerGibibyte).ToString("F3")).Append(" GiB\n");
    }
}

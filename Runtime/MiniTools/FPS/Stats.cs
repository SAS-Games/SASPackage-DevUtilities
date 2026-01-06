using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class Stats : UIBehaviour
{
    [SerializeField] private Text m_Display;
    [SerializeField] private float m_UpdateInterval = 0.5f;

    private float _timeLeft;
    private int _frames;
    private double _accumulatedFps;

    private readonly StringBuilder _sb = new StringBuilder(512);
    private readonly FrameTiming[] _frameTimings = new FrameTiming[1];

    private static readonly string FPS_RED = "<color=#FF0000>FPS: ";
    private static readonly string FPS_YELLOW = "<color=#FFFF00>FPS: ";
    private static readonly string FPS_GREEN = "<color=#00FF00>FPS: ";
    private static readonly string COLOR_END = "</color>\n";

    protected override void Awake()
    {
#if UNITY_EDITOR
        enabled = true;
#else
        enabled = Debug.isDebugBuild;
#endif
    }

    protected override void Start()
    {
        _timeLeft = m_UpdateInterval;
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();

        _timeLeft -= Time.deltaTime;
        _accumulatedFps += 1.0 / Time.deltaTime;
        _frames++;

        if (_timeLeft > 0f)
            return;

        if (FrameTimingManager.GetLatestTimings(1, _frameTimings) == 0)
            return;

        double avgFps = _accumulatedFps / _frames;

        _sb.Length = 0;

        // --- FPS ---
        if (avgFps < 10)
            _sb.Append(FPS_RED);
        else if (avgFps < 30)
            _sb.Append(FPS_YELLOW);
        else
            _sb.Append(FPS_GREEN);

        _sb.Append(avgFps.ToString("F1"));
        _sb.Append(COLOR_END);

        // --- Timings ---
        _sb.Append("Frame Time ");
        _sb.Append(_frameTimings[0].cpuFrameTime.ToString("F3"));
        _sb.Append(" ms\n");

        _sb.Append("CPU Main ");
        _sb.Append(_frameTimings[0].cpuMainThreadFrameTime.ToString("F3"));
        _sb.Append(" ms\n");

        _sb.Append("CPU Render ");
        _sb.Append(_frameTimings[0].cpuRenderThreadFrameTime.ToString("F3"));
        _sb.Append(" ms\n");

        _sb.Append("CPU Present ");
        _sb.Append(_frameTimings[0].cpuMainThreadPresentWaitTime.ToString("F3"));
        _sb.Append(" ms\n");

        _sb.Append("GPU ");
        _sb.Append(_frameTimings[0].gpuFrameTime.ToString("F3"));
        _sb.Append(" ms\n");

        // --- Memory ---
        _sb.Append("Allocated ");
        _sb.Append((Profiler.GetTotalAllocatedMemoryLong() / 1073741824f).ToString("F3"));
        _sb.Append(" GB\n");

        _sb.Append("Reserved ");
        _sb.Append((Profiler.GetTotalReservedMemoryLong() / 1073741824f).ToString("F3"));
        _sb.Append(" GB\n");

        _sb.Append("Unused ");
        _sb.Append((Profiler.GetTotalUnusedReservedMemoryLong() / 1073741824f).ToString("F3"));
        _sb.Append(" GB\n");

        m_Display.text = _sb.ToString();

        _accumulatedFps = 0;
        _frames = 0;
        _timeLeft = m_UpdateInterval;
    }
}

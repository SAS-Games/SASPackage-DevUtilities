using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;

public class Stats : UIBehaviour
{
    [Header("Display")][SerializeField] private Text m_Display = default;
    [SerializeField] private float m_UpdateInterval = 0.5f;

    private float _timeLeft;
    private int _frames;
    private double _accumulatedFps;

    private readonly StringBuilder tx = new StringBuilder(512);
    private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
    private string fpsLine = "";

    protected override void Awake()
    {
        base.Awake();

#if UNITY_EDITOR
        var fps = GetComponent<FPS>();
        if (fps) fps.enabled = false;
        enabled = true;
#else
        // In build: enable only if Development Build
        enabled = Debug.isDebugBuild;
#endif
    }

    protected override void Start()
    {
        _timeLeft = m_UpdateInterval;
        _frames = 0;
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        _timeLeft -= Time.deltaTime;
        float currentFPS = Time.timeScale / Time.deltaTime;
        _accumulatedFps += currentFPS;
        _frames++;

        if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
        {
            if (_timeLeft <= 0)
            {
                double avgFps = _accumulatedFps / _frames;

                // --- FPS Color ---
                Color fpsColor = avgFps < 30 ? (avgFps < 10 ? Color.red : Color.yellow) : Color.green;
                string fpsHex = ColorUtility.ToHtmlStringRGB(fpsColor);
                fpsLine = $"<color=#{fpsHex}>FPS: {avgFps:F1}</color>";

                tx.Length = 0;
                tx.AppendFormat(fpsLine + "\nFrame Time {0:F3} ms\n", _frameTimings[0].cpuFrameTime);
                tx.AppendFormat(
                  "CPU MainThread Frame {0:F3} ms\nCPU RenderThread Frame {1:F3} ms" +
                  "\nCPU Present Wait {2:F3} ms\nGPU Frame {3:F3} ms\n",
                  _frameTimings[0].cpuMainThreadFrameTime,
                  _frameTimings[0].cpuRenderThreadFrameTime,
                  _frameTimings[0].cpuMainThreadPresentWaitTime,
                  _frameTimings[0].gpuFrameTime
                );
                tx.AppendFormat(
                  "Allocated: {0:F3} GB\nReserved: {1:F3} GB\nUnused: {2:F3} GB\n",
                  Profiler.GetTotalAllocatedMemoryLong() / 1073741824f,
                  Profiler.GetTotalReservedMemoryLong() / 1073741824f,
                  Profiler.GetTotalUnusedReservedMemoryLong() / 1073741824f
                );

                _accumulatedFps = 0;
                _frames = 0;
                _timeLeft = m_UpdateInterval;
            }

            m_Display.text = $"{tx}";
        }
    }
}
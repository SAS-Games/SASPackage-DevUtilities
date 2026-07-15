using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lightweight legacy FPS display. New overlays should use <see cref="Stats"/>.
/// </summary>
public class FPS : UIBehaviour
{
    private const float MinimumUpdateInterval = 0.05f;

    [Header("Display")]
    [SerializeField] private Text m_Display;
    [Min(MinimumUpdateInterval)]
    [SerializeField] private float m_UpdateInterval = 0.5f;
    [SerializeField] private int m_TargetFrameRate = 60;

    private double _elapsedSeconds;
    private int _frames;

    private readonly StringBuilder _builder = new StringBuilder(128);

    private const string ColorRed = "<color=#FF0000>";
    private const string ColorYellow = "<color=#FFFF00>";
    private const string ColorWhite = "<color=#FFFFFF>";
    private const string ColorGreen = "<color=#00FF00>";
    private const string ColorEnd = "</color>";

    protected override void Awake()
    {
        base.Awake();
#if UNITY_EDITOR
        enabled = false;
#else
        enabled = !Debug.isDebugBuild; 
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

        _elapsedSeconds += deltaTime;
        _frames++;

        double updateInterval = Mathf.Max(MinimumUpdateInterval, m_UpdateInterval);
        if (_elapsedSeconds < updateInterval)
            return;

        double averageFps = _frames / _elapsedSeconds;
        double frameTimeMs = _elapsedSeconds * 1000d / _frames;
        int targetFrameRate = Application.targetFrameRate > 0
            ? Application.targetFrameRate
            : Mathf.Max(1, m_TargetFrameRate);
        double targetFrameTimeMs = 1000d / targetFrameRate;

        _builder.Length = 0;

        if (averageFps < 10d)
            _builder.Append(ColorRed);
        else if (averageFps < 30d)
            _builder.Append(ColorYellow);
        else
            _builder.Append(ColorGreen);

        _builder.Append("FPS: ")
            .Append(averageFps.ToString("F1"))
            .Append(ColorEnd)
            .Append('\n');

        _builder.Append(frameTimeMs > targetFrameTimeMs ? ColorRed : ColorWhite)
            .Append("Frame Time: ")
            .Append(frameTimeMs.ToString("F2"))
            .Append(" ms")
            .Append(ColorEnd);

        m_Display.text = _builder.ToString();
        ResetSample();
#endif
    }

    private void ResetSample()
    {
        _elapsedSeconds = 0d;
        _frames = 0;
    }
}

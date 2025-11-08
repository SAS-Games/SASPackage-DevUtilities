using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FPS : UIBehaviour
{
    [Header("Display")]
    [SerializeField] private Text m_Display = default;
    [SerializeField] private float m_UpdateInterval = 0.5f;
    [SerializeField] private int m_TargetFrameRate = 60;

    private float _timeLeft;
    private int _frames;
    private float _accumulatedFPS;

    private float _framesAvgTick;
    private float _framesAvg;
    
    protected override void Start()
    {
        _timeLeft = m_UpdateInterval;
        _frames = 0;
        _framesAvg = 0f;
        _framesAvgTick = 0f;
    }

    private void Update()
    {
        _timeLeft -= Time.deltaTime;
        float currentFPS = Time.timeScale / Time.deltaTime;
        _accumulatedFPS += currentFPS;
        _frames++;

        if (_timeLeft <= 0)
        {
            float avgFps = _accumulatedFPS / _frames;
            float frameTimeMs = 1000f / Mathf.Max(avgFps, 0.0001f);
            float targetFrameTime = 1000f / Mathf.Max(Application.targetFrameRate, 0.0001f);

            _framesAvgTick++;
            _framesAvg += avgFps;
            float fpsav = _framesAvg / _framesAvgTick;

            // --- FPS Color ---
            Color fpsColor = avgFps < 30 ? (avgFps < 10 ? Color.red : Color.yellow) : Color.green;
            string fpsHex = ColorUtility.ToHtmlStringRGB(fpsColor);
            string fpsLine = $"<color=#{fpsHex}>FPS: {avgFps:F1}</color>";
            
            m_Display.text = $"{fpsLine}\n" + $"{Colorize("Frame Time", frameTimeMs, "ms", Color.white)}";

            _accumulatedFPS = 0;
            _frames = 0;
            _timeLeft = m_UpdateInterval;
        }
    }

    private string Colorize(string label, float value, string unit, Color baseColor)
    {
        float targetFrameTime = 1000f / Mathf.Max(m_TargetFrameRate, 0.0001f);
        Color color = value > targetFrameTime ? Color.red : baseColor;
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{hex}>{label}: {value:F2} {unit}</color>";
    }
}

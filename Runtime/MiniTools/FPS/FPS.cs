using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class FPS : UIBehaviour
{
    [Header("Display")]
    [SerializeField] private Text m_Display;
    [SerializeField] private float m_UpdateInterval = 0.5f;
    [SerializeField] private int m_TargetFrameRate = 60;

    private float _timeLeft;
    private int _frames;
    private float _accumulatedFPS;

    private readonly StringBuilder _sb = new StringBuilder(128);

    private static readonly string COLOR_RED = "<color=#FF0000>";
    private static readonly string COLOR_YELLOW = "<color=#FFFF00>";
    private static readonly string COLOR_WHITE = "<color=#FFFFFF>";
    private static readonly string COLOR_GREEN = "<color=#00FF00>";
    private static readonly string COLOR_END = "</color>";

    protected override void Awake()
    {
#if UNITY_EDITOR
        enabled = false;
#else
        enabled = Debug.isDebugBuild;
#endif
    }

    protected override void Start()
    {
        _timeLeft = m_UpdateInterval;
        _frames = 0;
        _accumulatedFPS = 0f;
    }

    private void Update()
    {
        _timeLeft -= Time.deltaTime;

        _accumulatedFPS += 1f / Time.deltaTime;
        _frames++;

        if (_timeLeft > 0f)
            return;

        float avgFps = _accumulatedFPS / _frames;
        float frameTimeMs = 1000f / Mathf.Max(avgFps, 0.0001f);
        float targetFrameTime = 1000f / Mathf.Max(m_TargetFrameRate, 0.0001f);

        _sb.Length = 0;

        if (avgFps < 10f)
            _sb.Append(COLOR_RED);
        else if (avgFps < 30f)
            _sb.Append(COLOR_YELLOW);
        else
            _sb.Append(COLOR_GREEN);

        _sb.Append("FPS: ");
        _sb.Append(avgFps.ToString("F1"));
        _sb.Append(COLOR_END);
        _sb.Append('\n');

        if (frameTimeMs > targetFrameTime)
            _sb.Append(COLOR_RED);
        else
            _sb.Append(COLOR_WHITE);

        _sb.Append("Frame Time: ");
        _sb.Append(frameTimeMs.ToString("F2"));
        _sb.Append(" ms");
        _sb.Append(COLOR_END);

        m_Display.text = _sb.ToString();

        _accumulatedFPS = 0f;
        _frames = 0;
        _timeLeft = m_UpdateInterval;
    }
}

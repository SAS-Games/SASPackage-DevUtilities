#if ENABLE_DEBUG
using System;
using System.Text;
using UnityEngine;

public sealed class InputLatencyOverlay : IDisposable
{
    private const int VisibleSamples = 12;
    private readonly InputLatencyStatistics _raw;
    private readonly InputLatencyStatistics _action;
    private readonly InputLatencyStatistics _dispatch;
    private readonly InputLatencyStatistics _userMethod;
    private readonly StringBuilder _builder = new(192);

    private Rect _window = new(24f, 24f, 920f, 590f);
    private Vector2 _actionScroll;
    private Vector2 _userMethodScroll;
    private GUIStyle _windowStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    private GUIStyle _cardStyle;
    private GUIStyle _mutedStyle;
    private GUIStyle _rowStyle;
    private Texture2D _windowTexture;
    private Texture2D _cardTexture;

    public InputLatencyOverlay(
        InputLatencyStatistics raw,
        InputLatencyStatistics action,
        InputLatencyStatistics dispatch,
        InputLatencyStatistics userMethod)
    {
        _raw = raw;
        _action = action;
        _dispatch = dispatch;
        _userMethod = userMethod;
    }

    public void Draw()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        EnsureStyles();
        _window.width = Mathf.Clamp(_window.width, 680f, Mathf.Max(680f, Screen.width - 16f));
        _window.height = Mathf.Clamp(_window.height, 420f, Mathf.Max(420f, Screen.height - 16f));
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));
        _window = GUI.Window(0x1A71E, _window, DrawWindow, GUIContent.none, _windowStyle);
#endif
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("INPUT LATENCY", _headerStyle);
        GUILayout.Label("Input event timestamp to callback and user-method entry", _mutedStyle);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label(InputStateLabel(), _mutedStyle);
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        DrawMetricCard("ACTION CALLBACK", _action, new Color(0.22f, 0.75f, 1f));
        DrawMetricCard("EVENT QUEUE", _raw, new Color(0.45f, 0.9f, 0.55f));
        DrawMetricCard("INPUT DISPATCH", _dispatch, new Color(0.85f, 0.55f, 1f));
        DrawMetricCard("USER METHOD", _userMethod, new Color(1f, 0.67f, 0.28f));
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width((_window.width - 38f) * 0.56f));
        GUILayout.Label("RECENT ACTION CALLBACKS", _subHeaderStyle);
        _actionScroll = GUILayout.BeginScrollView(_actionScroll, _cardStyle);
        DrawActionRows();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(8f);
        GUILayout.BeginVertical();
        GUILayout.Label("RECENT USER METHOD MARKERS", _subHeaderStyle);
        _userMethodScroll = GUILayout.BeginScrollView(_userMethodScroll, _cardStyle);
        DrawUserMethodRows();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUILayout.Space(5f);
        GUILayout.Label("Event Queue + Input Dispatch = Action Callback. User Method is measured where InputLatencyProfilerMarker.Measure is called.", _mutedStyle);
        GUI.DragWindow(new Rect(0f, 0f, _window.width, 55f));
    }

    private void DrawMetricCard(string title, InputLatencyStatistics source, Color accent)
    {
        Color previous = GUI.color;
        GUILayout.BeginVertical(_cardStyle, GUILayout.ExpandWidth(true), GUILayout.Height(78f));
        GUI.color = accent;
        GUILayout.Label(title, _subHeaderStyle);
        GUI.color = Color.white;
        GUILayout.Label(source.SampleCount == 0 ? "-- ms" : $"{source.Average:F2} ms", _headerStyle);
        GUILayout.Label($"min {source.Min:F2}  max {source.Max:F2}  n {source.SampleCount}", _mutedStyle);
        GUILayout.EndVertical();
        GUI.color = previous;
    }

    private void DrawActionRows()
    {
        GUILayout.BeginHorizontal(_rowStyle);
        GUILayout.Label("Action", _mutedStyle, GUILayout.Width(135f));
        GUILayout.Label("Phase", _mutedStyle, GUILayout.Width(72f));
        GUILayout.Label("Control", _mutedStyle);
        GUILayout.Label("Latency", _mutedStyle, GUILayout.Width(66f));
        GUILayout.EndHorizontal();
        int count = Mathf.Min(VisibleSamples, _action.SampleCount);
        for (int i = 0; i < count; i++)
        {
            ref readonly InputLatencySample sample = ref _action.GetRecentSample(i);
            GUILayout.BeginHorizontal(_rowStyle);
            GUILayout.Label(sample.ActionName, GUILayout.Width(135f));
            GUILayout.Label(sample.Phase.ToString(), GUILayout.Width(72f));
            GUILayout.Label(ShortControl(sample.ControlName));
            GUILayout.Label($"{sample.LatencyMs:F2} ms", GUILayout.Width(66f));
            GUILayout.EndHorizontal();
        }
        if (count == 0) GUILayout.Label("Waiting for an InputAction callback...", _mutedStyle);
    }

    private void DrawUserMethodRows()
    {
        GUILayout.BeginHorizontal(_rowStyle);
        GUILayout.Label("Marker", _mutedStyle);
        GUILayout.Label("Phase", _mutedStyle, GUILayout.Width(72f));
        GUILayout.Label("Latency", _mutedStyle, GUILayout.Width(66f));
        GUILayout.EndHorizontal();
        int count = Mathf.Min(VisibleSamples, _userMethod.SampleCount);
        for (int i = 0; i < count; i++)
        {
            ref readonly InputLatencySample sample = ref _userMethod.GetRecentSample(i);
            GUILayout.BeginHorizontal(_rowStyle);
            GUILayout.Label(sample.ActionName);
            GUILayout.Label(sample.Phase.ToString(), GUILayout.Width(72f));
            GUILayout.Label($"{sample.LatencyMs:F2} ms", GUILayout.Width(66f));
            GUILayout.EndHorizontal();
        }
        if (count == 0)
            GUILayout.Label("Add InputLatencyProfilerMarker.Measure at the first line of your callback.", _mutedStyle);
    }

    private string InputStateLabel()
    {
        _builder.Clear();
        _builder.Append("Update: ");
        _builder.Append(UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType);
        _builder.Append("  |  Frame ");
        _builder.Append(Time.frameCount);
        return _builder.ToString();
    }

    private static string ShortControl(string path)
    {
        if (string.IsNullOrEmpty(path)) return "Unknown";
        int slash = path.LastIndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : path;
    }

    private void EnsureStyles()
    {
        if (_windowStyle != null) return;
        _windowStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(16, 16, 14, 14) };
        _windowTexture = MakeTexture(new Color(0.055f, 0.065f, 0.085f, 0.98f));
        _windowStyle.normal.background = _windowTexture;
        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.92f, 0.95f, 1f) } };
        _subHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.72f, 0.78f, 0.88f) } };
        _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = new Color(0.55f, 0.61f, 0.7f) } };
        _cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        _cardTexture = MakeTexture(new Color(0.09f, 0.11f, 0.145f, 0.98f));
        _cardStyle.normal.background = _cardTexture;
        _rowStyle = new GUIStyle { padding = new RectOffset(4, 4, 3, 3) };
    }

    private static Texture2D MakeTexture(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    public void Dispose()
    {
        if (_windowTexture != null) UnityEngine.Object.Destroy(_windowTexture);
        if (_cardTexture != null) UnityEngine.Object.Destroy(_cardTexture);
        _windowTexture = null;
        _cardTexture = null;
        _windowStyle = null;
    }
}
#endif

#if ENABLE_DEBUG
using System;
using System.Text;
using UnityEngine;

public sealed class InputLatencyOverlay : IDisposable
{
    private const int VisibleSamples = 12;
    private const int GraphVisibleSamples = 64;
    private const int GraphRecentFrameWindow = 300;
    private const float MinimumGraphScaleMs = 25f;
    private const float MaximumGraphScaleMs = 500f;
    private const float SixtyFpsFrameMs = 1000f / 60f;
    private static readonly Color ActionColor = new(0.22f, 0.75f, 1f);
    private static readonly Color EventQueueColor = new(0.45f, 0.9f, 0.55f);
    private static readonly Color DispatchColor = new(0.85f, 0.55f, 1f);
    private static readonly Color UserMethodColor = new(1f, 0.67f, 0.28f);
    private readonly IInputLatencyOverlayModel _model;
    private readonly bool _controllerCloseEnabled;
    private readonly bool _showCloseHint;
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
    private GUIStyle _buttonStyle;
    private GUIStyle _graphLabelStyle;
    private GUIStyle _graphValueStyle;
    private Texture2D _windowTexture;
    private Texture2D _cardTexture;
    private Texture2D _buttonHoverTexture;
    private float _graphScaleMs = MinimumGraphScaleMs;
    private float _graphScaleDecreaseTime;
    private int _graphScaleUpdateFrame = -1;
    private bool _graphScaleClamped;
    private bool _graphVisible;

    internal InputLatencyOverlay(IInputLatencyOverlayModel model, bool controllerCloseEnabled, bool showCloseHint)
    {
        _model = model ??
                 throw new ArgumentNullException(nameof(model));
        _controllerCloseEnabled = controllerCloseEnabled;
        _showCloseHint = showCloseHint;
    }

    public void Draw()
    {
        EnsureStyles();
        _window.width = Mathf.Clamp(_window.width, 680f, Mathf.Max(680f, Screen.width - 16f));
        _window.height = Mathf.Clamp(_window.height, 420f, Mathf.Max(420f, Screen.height - 16f));
        _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
        _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));
        _window = GUI.Window(0x1A71E, _window, DrawWindow, GUIContent.none, _windowStyle);
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
        GUILayout.Space(8f);
        if (GUILayout.Button(_graphVisible ? "HIDE GRAPH" : "SHOW GRAPH", _buttonStyle,
                GUILayout.Width(86f), GUILayout.Height(26f)))
            _graphVisible = !_graphVisible;
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        GUILayout.BeginHorizontal();
        DrawMetricCard(
            "ACTION CALLBACK",
            _model.ActionStatistics,
            ActionColor);
        DrawMetricCard(
            "EVENT QUEUE",
            _model.RawStatistics,
            EventQueueColor);
        DrawMetricCard(
            "INPUT DISPATCH",
            _model.PipelineStatistics,
            DispatchColor);
        DrawMetricCard(
            "USER METHOD",
            _model.UserMethodStatistics,
            UserMethodColor);
        GUILayout.EndHorizontal();

        if (_graphVisible)
        {
            GUILayout.Space(10f);
            DrawLatencyGraphCard();
        }

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
        string closeHint = _showCloseHint
            ? _controllerCloseEnabled
                ? "  Esc / B closes."
                : "  Esc closes."
            : string.Empty;
        GUILayout.Label(
            "Event Queue + Input Dispatch = Action Callback. User Method is measured where " +
            "InputLatencyProfilerMarker.Measure is called." +
            closeHint,
            _mutedStyle);
        GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, _window.width - 180f), 55f));
    }

    private void DrawMetricCard(
        string title,
        IInputLatencyStatisticsSource source,
        Color accent)
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

    private void DrawLatencyGraphCard()
    {
        if (Event.current.type == EventType.Repaint)
            UpdateGraphScale(FindRecentGraphPeak());

        float graphHeight = _window.height < 520f ? 88f : 112f;
        GUILayout.BeginVertical(_cardStyle, GUILayout.Height(graphHeight + 36f));
        GUILayout.BeginHorizontal();
        GUILayout.Label("RECENT LATENCY", _subHeaderStyle);
        GUILayout.FlexibleSpace();
        string scaleSuffix = _graphScaleClamped ? "+" : string.Empty;
        GUILayout.Label(
            $"{GraphRecentFrameWindow}-frame expiry  |  max {GraphVisibleSamples}/metric  |  newest right  |  " +
            $"0-{_graphScaleMs:0}{scaleSuffix} ms  |  60 Hz ref 16.7 ms",
            _mutedStyle);
        GUILayout.EndHorizontal();

        Rect graphRect = GUILayoutUtility.GetRect(100f, graphHeight, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint)
            DrawLatencyGraph(graphRect);
        GUILayout.EndVertical();
    }

    private void DrawLatencyGraph(Rect area)
    {
        float labelWidth = Mathf.Clamp(area.width * 0.17f, 96f, 124f);
        const float valueWidth = 57f;
        Rect plotArea = new(area.x + labelWidth, area.y, Mathf.Max(1f, area.width - labelWidth - valueWidth),
            area.height);
        float rowHeight = area.height * 0.25f;

        DrawGraphRow(new Rect(area.x, area.y, area.width, rowHeight), "ACTION CALLBACK", _model.ActionStatistics,
            ActionColor,
            plotArea.x, plotArea.width);
        DrawGraphRow(new Rect(area.x, area.y + rowHeight, area.width, rowHeight), "EVENT QUEUE", _model.RawStatistics,
            EventQueueColor, plotArea.x, plotArea.width);
        DrawGraphRow(new Rect(area.x, area.y + rowHeight * 2f, area.width, rowHeight), "INPUT DISPATCH",
            _model.PipelineStatistics,
            DispatchColor, plotArea.x, plotArea.width);
        DrawGraphRow(new Rect(area.x, area.y + rowHeight * 3f, area.width, rowHeight), "USER METHOD",
            _model.UserMethodStatistics,
            UserMethodColor, plotArea.x, plotArea.width);
    }

    private void DrawGraphRow(
        Rect row,
        string label,
        IInputLatencyStatisticsSource source,
        Color color,
        float plotX,
        float plotWidth)
    {
        Rect plot = new(plotX, row.y + 2f, plotWidth, Mathf.Max(1f, row.height - 4f));
        DrawSolidRect(plot, new Color(0.025f, 0.03f, 0.045f, 0.5f));

        float halfY = plot.yMax - plot.height * 0.5f;
        DrawSolidRect(new Rect(plot.x, halfY, plot.width, 1f), new Color(1f, 1f, 1f, 0.035f));
        float frameGuideY = plot.yMax - plot.height * Mathf.Clamp01(SixtyFpsFrameMs / _graphScaleMs);
        DrawSolidRect(new Rect(plot.x, frameGuideY, plot.width, 1f), new Color(1f, 0.78f, 0.3f, 0.12f));

        Color previous = GUI.color;
        GUI.color = color;
        GUI.Label(new Rect(row.x, row.y, plot.x - row.x - 6f, row.height), label, _graphLabelStyle);
        GUI.color = previous;

        int capacityFromWidth = Mathf.Max(1, Mathf.FloorToInt(plot.width / 3f));
        int maximumCount = Mathf.Min(GraphVisibleSamples, capacityFromWidth);
        int count = GetRecentGraphSampleCount(source, maximumCount);
        if (count == 0)
        {
            string emptyMessage = source.SampleCount == 0 ? "waiting for samples" : "no recent activity";
            GUI.Label(new Rect(plot.x + 5f, row.y, plot.width - 10f, row.height), emptyMessage, _mutedStyle);
            GUI.Label(new Rect(plot.xMax + 5f, row.y, row.xMax - plot.xMax - 5f, row.height), "--",
                _graphValueStyle);
            return;
        }

        float slotWidth = plot.width / count;
        float barWidth = Mathf.Clamp(slotWidth * 0.62f, 1f, 6f);
        float availableX = Mathf.Max(0f, plot.width - barWidth);
        for (int displayIndex = 0; displayIndex < count; displayIndex++)
        {
            int recentIndex = count - 1 - displayIndex;
            float latencyMs = source.GetRecentSample(recentIndex).LatencyMs;
            float normalized = float.IsNaN(latencyMs) || float.IsInfinity(latencyMs)
                ? 0f
                : Mathf.Clamp01(latencyMs / _graphScaleMs);
            float height = Mathf.Max(1f, normalized * plot.height);
            float x = count == 1
                ? plot.xMax - barWidth
                : plot.x + availableX * displayIndex / (count - 1f);
            DrawSolidRect(new Rect(x, plot.yMax - height, barWidth, height), color);
        }

        float latest = source.GetRecentSample(0).LatencyMs;
        GUI.Label(new Rect(plot.xMax + 5f, row.y, row.xMax - plot.xMax - 5f, row.height), $"{latest:F1} ms",
            _graphValueStyle);
    }

    private float FindRecentPeak(
        IInputLatencyStatisticsSource source)
    {
        float peak = 0f;
        int count = GetRecentGraphSampleCount(source, GraphVisibleSamples);
        for (int i = 0; i < count; i++)
        {
            float latencyMs = source.GetRecentSample(i).LatencyMs;
            if (!float.IsNaN(latencyMs) && !float.IsInfinity(latencyMs) && latencyMs > peak)
                peak = latencyMs;
        }

        return peak;
    }

    private int GetRecentGraphSampleCount(
        IInputLatencyStatisticsSource source,
        int maximumCount)
    {
        int available = Mathf.Min(
            source.RecentSampleCount,
            maximumCount);
        int currentFrame = _model.CurrentFrame;
        int count = 0;
        for (int i = 0; i < available; i++)
        {
            int frameAge = currentFrame - source.GetRecentSample(i).Frame;
            if (frameAge >= GraphRecentFrameWindow)
                break;
            count++;
        }

        return count;
    }

    private float FindRecentGraphPeak()
    {
        float peak =
            FindRecentPeak(_model.ActionStatistics);
        peak = Mathf.Max(
            peak,
            FindRecentPeak(_model.RawStatistics));
        peak = Mathf.Max(
            peak,
            FindRecentPeak(_model.PipelineStatistics));
        return Mathf.Max(
            peak,
            FindRecentPeak(_model.UserMethodStatistics));
    }

    private void UpdateGraphScale(float peak)
    {
        if (_graphScaleUpdateFrame == Time.frameCount)
            return;

        _graphScaleUpdateFrame = Time.frameCount;
        float target = SelectGraphScale(peak * 1.15f);
        _graphScaleClamped = peak > MaximumGraphScaleMs;
        if (target > _graphScaleMs)
        {
            _graphScaleMs = target;
            _graphScaleDecreaseTime = Time.unscaledTime + 2f;
        }
        else if (target < _graphScaleMs && Time.unscaledTime >= _graphScaleDecreaseTime)
        {
            _graphScaleMs = target;
            _graphScaleDecreaseTime = Time.unscaledTime + 2f;
        }
    }

    private static float SelectGraphScale(float requiredMs)
    {
        if (requiredMs <= 25f) return MinimumGraphScaleMs;
        if (requiredMs <= 50f) return 50f;
        if (requiredMs <= 100f) return 100f;
        if (requiredMs <= 200f) return 200f;
        return MaximumGraphScaleMs;
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
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
        int count = Mathf.Min(
            VisibleSamples,
            _model.ActionStatistics.RecentSampleCount);
        for (int i = 0; i < count; i++)
        {
            ref readonly InputLatencySample sample =
                ref _model.ActionStatistics.GetRecentSample(i);
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
        int count = Mathf.Min(
            VisibleSamples,
            _model.UserMethodStatistics.RecentSampleCount);
        for (int i = 0; i < count; i++)
        {
            ref readonly InputLatencySample sample =
                ref _model.UserMethodStatistics.GetRecentSample(i);
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
        _builder.Append(_model.CurrentUpdateType);
        _builder.Append("  |  Frame ");
        _builder.Append(_model.CurrentFrame);
        if (_model.DroppedSampleCount > 0)
        {
            _builder.Append("  |  Dropped ");
            _builder.Append(_model.DroppedSampleCount);
        }

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
        _windowTexture = MakeTexture(new Color(0.055f, 0.065f, 0.085f, 0.86f));
        _windowStyle.normal.background = _windowTexture;
        _headerStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.92f, 0.95f, 1f) } };
        _subHeaderStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.72f, 0.78f, 0.88f) } };
        _mutedStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 10, normal = { textColor = new Color(0.55f, 0.61f, 0.7f) } };
        _cardStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        _cardTexture = MakeTexture(new Color(0.09f, 0.11f, 0.145f, 0.72f));
        _cardStyle.normal.background = _cardTexture;
        _buttonHoverTexture = MakeTexture(new Color(0.12f, 0.32f, 0.43f, 0.9f));
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = _cardTexture, textColor = new Color(0.72f, 0.78f, 0.88f) },
            hover = { background = _buttonHoverTexture, textColor = Color.white },
            active = { background = _buttonHoverTexture, textColor = Color.white },
            focused = { background = _buttonHoverTexture, textColor = Color.white }
        };
        _rowStyle = new GUIStyle { padding = new RectOffset(4, 4, 3, 3) };
        _graphLabelStyle = new GUIStyle(_mutedStyle)
        {
            fontSize = 9,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };
        _graphValueStyle = new GUIStyle(_mutedStyle)
        {
            fontSize = 9,
            alignment = TextAnchor.MiddleRight,
            clipping = TextClipping.Clip
        };
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
        if (_buttonHoverTexture != null) UnityEngine.Object.Destroy(_buttonHoverTexture);
        _windowTexture = null;
        _cardTexture = null;
        _buttonHoverTexture = null;
        _windowStyle = null;
        _buttonStyle = null;
        _graphLabelStyle = null;
        _graphValueStyle = null;
    }
}
#endif

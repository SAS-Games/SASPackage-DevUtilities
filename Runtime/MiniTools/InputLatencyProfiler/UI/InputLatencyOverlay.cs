using System.Text;
using UnityEngine;

public sealed class InputLatencyOverlay
{
    private const int VisibleSamples = 15;
    private readonly InputLatencyStatistics _rawStatistics;
    private readonly InputLatencyStatistics _actionStatistics;
    private readonly StringBuilder _builder = new StringBuilder(256);

    public InputLatencyOverlay(InputLatencyStatistics rawStatistics, InputLatencyStatistics actionStatistics)
    {
        _rawStatistics = rawStatistics;
        _actionStatistics = actionStatistics;
    }

    public void Draw()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        DrawRawColumn();
        DrawActionColumn();
#endif
    }
    
    private void DrawRawColumn()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 450), GUI.skin.box);

        GUILayout.Label("RAW INPUT EVENTS");
        GUILayout.Space(5);
        GUILayout.Label($"Samples : {_rawStatistics.SampleCount}");

        GUILayout.Space(10);

        int visible = Mathf.Min(VisibleSamples, _rawStatistics.SampleCount);
        
        for (int i = 0; i < visible; i++)
        {
            ref readonly InputLatencySample sample = ref _rawStatistics.GetRecentSample(i);

            _builder.Clear();

            _builder.Append('[');
            _builder.Append(sample.Frame);
            _builder.Append("] ");

            _builder.Append(sample.ControlName);
            _builder.Append(" | ");

            _builder.Append(sample.LatencyMs.ToString("F2"));
            _builder.Append(" ms | ");

            _builder.Append(sample.UpdateType);

            GUILayout.Label(_builder.ToString());
        }

        GUILayout.EndArea();
    }
    private void DrawActionColumn()
    {
        GUILayout.BeginArea(new Rect(320, 10, 500, 450), GUI.skin.box);

        GUILayout.Label("ACTION INPUT EVENTS");
        GUILayout.Space(5);
        GUILayout.Label($"Samples : {_actionStatistics.SampleCount}");
        GUILayout.Space(10);

        int visible = Mathf.Min(VisibleSamples, _actionStatistics.SampleCount);
        
        for (int i = 0; i < visible; i++)
        {
            ref readonly InputLatencySample sample = ref _actionStatistics.GetRecentSample(i);

            _builder.Clear();

            _builder.Append('[');
            _builder.Append(sample.Frame);
            _builder.Append("] ");

            _builder.Append(sample.ActionName);
            _builder.Append(" | ");

            _builder.Append(sample.Phase);
            _builder.Append(" | ");

            _builder.Append(sample.ControlName);
            _builder.Append(" | ");

            _builder.Append(sample.LatencyMs.ToString("F2"));
            _builder.Append(" ms | ");

            _builder.Append(sample.UpdateType);

            GUILayout.Label(_builder.ToString());
        }

        GUILayout.EndArea();
    }
}
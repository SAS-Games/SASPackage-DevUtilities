#if ENABLE_DEBUG
public sealed class InputLatencyStatistics : IGraphStatisticsSource
{
    public float MinLatency { get; private set; }
    public float Min => MinLatency;
    public float Average => AverageLatency;
    public float Max => MaxLatency;

    private readonly CircularBuffer<InputLatencySample> _samples;

    private double _totalLatency;

    public float AverageLatency { get; private set; }

    public float MaxLatency { get; private set; }

    public int SampleCount => _samples.Count;

    public InputLatencyStatistics(int capacity)
    {
        _samples = new CircularBuffer<InputLatencySample>(capacity);
    }

    public void AddSample(InputLatencySample sample)
    {
        bool overwritten = _samples.Add(sample, out InputLatencySample removed);
        if (overwritten)
            _totalLatency -= removed.LatencyMs;
        _totalLatency += sample.LatencyMs;
        AverageLatency = (float)(_totalLatency / _samples.Count);

        if (_samples.Count == 1)
            MinLatency = MaxLatency = sample.LatencyMs;
        else if (overwritten && (removed.LatencyMs <= MinLatency || removed.LatencyMs >= MaxLatency))
            RecalculateRange();
        else
        {
            if (sample.LatencyMs < MinLatency) MinLatency = sample.LatencyMs;
            if (sample.LatencyMs > MaxLatency) MaxLatency = sample.LatencyMs;
        }
    }

    public ref readonly InputLatencySample
        GetRecentSample(int index)
    {
        return ref _samples.GetRecent(index);
    }

    private void RecalculateRange()
    {
        if (_samples.Count == 0) { MinLatency = MaxLatency = 0f; return; }
        MinLatency = float.MaxValue; MaxLatency = float.MinValue;
        for (int i = 0; i < _samples.Count; i++)
        {
            float value = _samples.GetRecent(i).LatencyMs;
            if (value < MinLatency) MinLatency = value;
            if (value > MaxLatency) MaxLatency = value;
        }
    }
}
#endif

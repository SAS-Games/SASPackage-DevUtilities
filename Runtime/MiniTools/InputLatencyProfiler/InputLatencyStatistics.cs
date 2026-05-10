public sealed class InputLatencyStatistics : IGraphStatisticsSource
{
    public float MinLatency { get; private set; }
    public float Min => MinLatency;
    public float Average => AverageLatency;
    public float Max => MaxLatency;

    private readonly CircularBuffer<InputLatencySample> _samples;

    private float _totalLatency;

    public float AverageLatency { get; private set; }

    public float MaxLatency { get; private set; }

    public int SampleCount => _samples.Count;

    public InputLatencyStatistics(int capacity)
    {
        _samples = new CircularBuffer<InputLatencySample>(capacity);
    }

    public void AddSample(InputLatencySample sample)
    {
        _samples.Add(sample);

        _totalLatency += sample.LatencyMs;

        AverageLatency =
            _totalLatency / _samples.Count;

        if (sample.LatencyMs > MaxLatency)
            MaxLatency = sample.LatencyMs;
    }

    public ref readonly InputLatencySample
        GetRecentSample(int index)
    {
        return ref _samples.GetRecent(index);
    }
}
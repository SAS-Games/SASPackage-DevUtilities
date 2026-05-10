public sealed class LatencyGraphSource : IGraphDataSource
{
    private readonly InputLatencyStatistics _statistics;

    public LatencyGraphSource(InputLatencyStatistics statistics, float maxValue)
    {
        _statistics = statistics;
        MaxValue = maxValue;
    }

    public int Count => _statistics.SampleCount;

    public float GetValue(int index)
    {
        return _statistics.GetRecentSample(index).LatencyMs;
    }

    public float MinValue => 0f;
    public float MaxValue { get; }
}
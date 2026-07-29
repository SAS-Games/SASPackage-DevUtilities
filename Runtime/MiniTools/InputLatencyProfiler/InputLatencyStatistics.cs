#if ENABLE_DEBUG
using System;
using System.Collections.Generic;

public interface IInputLatencyStatisticsSource
{
    float Average { get; }
    float Min { get; }
    float Max { get; }
    int SampleCount { get; }
    int RecentSampleCount { get; }

    ref readonly InputLatencySample GetRecentSample(int index);
}

public sealed class InputLatencyStatistics : IInputLatencyStatisticsSource
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
    public int RecentSampleCount => _samples.Count;
    internal long TotalSampleCount { get; private set; }

    public InputLatencyStatistics(int capacity)
    {
        _samples = new CircularBuffer<InputLatencySample>(capacity);
    }

    public void AddSample(InputLatencySample sample)
    {
        bool overwritten = _samples.Add(sample, out InputLatencySample removed);
        TotalSampleCount++;
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

    internal void CopySamplesSince(ref long cursor, ICollection<InputLatencySample> destination, int maximumCount, out int droppedSampleCount)
    {
        droppedSampleCount = 0;
        long total = TotalSampleCount;
        if (cursor >= total || total == 0)
        {
            cursor = total;
            return;
        }

        long earliestAvailable =
            total - _samples.Count + 1;
        long firstRequested = cursor + 1;
        if (firstRequested < earliestAvailable)
        {
            AddDropped(
                ref droppedSampleCount,
                earliestAvailable - firstRequested);
            firstRequested = earliestAvailable;
        }

        int boundedMaximum = Math.Max(1, maximumCount);
        long available = total - firstRequested + 1;
        if (available > boundedMaximum)
        {
            long skipped = available - boundedMaximum;
            AddDropped(ref droppedSampleCount, skipped);
            firstRequested += skipped;
        }

        for (long sequence = firstRequested;
             sequence <= total;
             sequence++)
        {
            int recentIndex =
                checked((int)(total - sequence));
            destination.Add(
                GetRecentSample(recentIndex));
        }

        cursor = total;
    }

    private static void AddDropped(
        ref int destination,
        long count)
    {
        destination = (int)Math.Min(
            int.MaxValue,
            (long)destination + Math.Max(0L, count));
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

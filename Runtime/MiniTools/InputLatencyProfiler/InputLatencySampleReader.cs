#if ENABLE_DEBUG
using System;
using System.Collections.Generic;

/// <summary>
/// Maintains independent cursors over the shared latency collector and
/// produces portable, bounded event batches.
/// </summary>
internal sealed class InputLatencySampleReader
{
    private const int MaximumSamplesPerMetricPerBatch = 128;

    private readonly List<InputLatencySample> _samples =
        new List<InputLatencySample>(256);

    private InputLatencyCollector _collector;
    private long _rawCursor;
    private long _actionCursor;
    private long _pipelineCursor;
    private long _userMethodCursor;

    internal void Reset(
        InputLatencyCollector collector,
        bool includeExistingSamples)
    {
        _collector = collector;
        _samples.Clear();
        _rawCursor = InitialCursor(
            collector?.RawStatistics,
            includeExistingSamples);
        _actionCursor = InitialCursor(
            collector?.ActionStatistics,
            includeExistingSamples);
        _pipelineCursor = InitialCursor(
            collector?.PipelineStatistics,
            includeExistingSamples);
        _userMethodCursor = InitialCursor(
            collector?.UserMethodStatistics,
            includeExistingSamples);
    }

    internal bool TryRead(
        out InputLatencySampleEvent[] events,
        out int droppedEventCount)
    {
        events = Array.Empty<InputLatencySampleEvent>();
        droppedEventCount = 0;
        if (_collector == null)
            return false;

        _samples.Clear();
        CopySamples(
            _collector.RawStatistics,
            ref _rawCursor,
            ref droppedEventCount);
        CopySamples(
            _collector.ActionStatistics,
            ref _actionCursor,
            ref droppedEventCount);
        CopySamples(
            _collector.PipelineStatistics,
            ref _pipelineCursor,
            ref droppedEventCount);
        CopySamples(
            _collector.UserMethodStatistics,
            ref _userMethodCursor,
            ref droppedEventCount);

        if (_samples.Count == 0)
            return false;

        _samples.Sort(CompareSamples);
        events = new InputLatencySampleEvent[_samples.Count];
        for (int i = 0; i < _samples.Count; i++)
        {
            InputLatencySample sample = _samples[i];
            events[i] =
                InputLatencySampleEvent.FromSample(in sample);
        }

        return true;
    }

    private void CopySamples(
        InputLatencyStatistics statistics,
        ref long cursor,
        ref int droppedEventCount)
    {
        statistics.CopySamplesSince(
            ref cursor,
            _samples,
            MaximumSamplesPerMetricPerBatch,
            out int dropped);
        droppedEventCount = (int)Math.Min(
            int.MaxValue,
            (long)droppedEventCount + dropped);
    }

    private static long InitialCursor(
        InputLatencyStatistics statistics,
        bool includeExistingSamples)
    {
        if (statistics == null)
            return 0;

        return includeExistingSamples
            ? Math.Max(
                0,
                statistics.TotalSampleCount -
                statistics.RecentSampleCount)
            : statistics.TotalSampleCount;
    }

    private static int CompareSamples(
        InputLatencySample left,
        InputLatencySample right)
    {
        int frameComparison =
            left.Frame.CompareTo(right.Frame);
        return frameComparison != 0
            ? frameComparison
            : ((int)left.EventType).CompareTo(
                (int)right.EventType);
    }
}
#endif

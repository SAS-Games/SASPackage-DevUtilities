using System;
using SAS.DevUtilities;

/// <summary>
/// Publishes Input Latency snapshots and incremental samples for the local Player
/// presentation. It contains no rendering or close-input logic.
/// </summary>
public sealed class InputLatencySnapshotProvider :
    MiniToolStreamingSnapshotProviderBehaviour<
        InputLatencySnapshot,
        InputLatencySampleEvent>
{
#if ENABLE_DEBUG
    private readonly InputLatencySampleReader _sampleReader =
        new InputLatencySampleReader();

    private InputLatencyCollector _collector;

    private void OnEnable()
    {
        _collector = InputLatencyCollectorPool.Acquire();
        _sampleReader.Reset(
            _collector,
            true);
        RefreshSnapshot();
    }

    private void OnDisable()
    {
        InputLatencyCollectorPool.Release(_collector);
        _collector = null;
        _sampleReader.Reset(null, false);
        ClearSnapshot();
    }

    private void Update()
    {
        RefreshSnapshot();
        if (TryGetEvents(
                out InputLatencySampleEvent[] events,
                out int droppedEventCount))
        {
            PublishEvents(
                events,
                droppedEventCount);
        }
    }

    public override bool TryGetEvents(
        out InputLatencySampleEvent[] events,
        out int droppedEventCount)
    {
        return _sampleReader.TryRead(
            out events,
            out droppedEventCount);
    }

    private void RefreshSnapshot()
    {
        InputLatencySnapshot snapshot =
            InputLatencySnapshot.Capture(_collector);
        PublishSnapshot(in snapshot);
    }
#else
    private void OnEnable()
    {
        InputLatencySnapshot snapshot = new InputLatencySnapshot
        {
            IsAvailable = false,
            Status = "Input Latency requires ENABLE_DEBUG."
        };
        PublishSnapshot(in snapshot);
    }

    public override bool TryGetEvents(
        out InputLatencySampleEvent[] events,
        out int droppedEventCount)
    {
        events = Array.Empty<InputLatencySampleEvent>();
        droppedEventCount = 0;
        return false;
    }
#endif
}

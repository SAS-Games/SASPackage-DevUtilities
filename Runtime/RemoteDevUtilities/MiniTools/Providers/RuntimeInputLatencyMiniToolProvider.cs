using System;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeInputLatencyMiniToolProvider :
        MiniToolStreamingDataProvider<
            InputLatencySnapshot,
            InputLatencySampleEvent>,
        IMiniToolFieldProvider
    {
#if ENABLE_DEBUG
        private readonly InputLatencySampleReader _sampleReader =
            new InputLatencySampleReader();

        private InputLatencyCollector _collector;

        public override void Start()
        {
            Stop();
            _collector = InputLatencyCollectorPool.Acquire();
            _sampleReader.Reset(
                _collector,
                false);
        }

        public override void Stop()
        {
            InputLatencyCollectorPool.Release(_collector);
            _collector = null;
            _sampleReader.Reset(null, false);
        }

        public override bool TryGetSnapshot(
            out InputLatencySnapshot snapshot)
        {
            snapshot = InputLatencySnapshot.Capture(_collector);
            return true;
        }

        public override bool TryGetEvents(
            out InputLatencySampleEvent[] events,
            out int droppedEventCount)
        {
            return _sampleReader.TryRead(
                out events,
                out droppedEventCount);
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            return new[]
            {
                AverageField(
                    "action",
                    "Action Callback",
                    _collector?.ActionStatistics),
                AverageField(
                    "eventQueue",
                    "Event Queue",
                    _collector?.RawStatistics),
                AverageField(
                    "dispatch",
                    "Input Dispatch",
                    _collector?.PipelineStatistics),
                AverageField(
                    "userMethod",
                    "User Method",
                    _collector?.UserMethodStatistics),
                CountField(
                    "samples",
                    "Action Samples",
                    _collector?.ActionStatistics)
            };
        }

        private static RemoteMiniToolField AverageField(
            string name,
            string displayName,
            InputLatencyStatistics statistics)
        {
            return CreateField(
                name,
                displayName,
                statistics == null ||
                statistics.SampleCount == 0
                    ? "0.00"
                    : statistics.Average.ToString("F2"),
                "ms");
        }

        private static RemoteMiniToolField CountField(
            string name,
            string displayName,
            InputLatencyStatistics statistics)
        {
            return CreateField(
                name,
                displayName,
                (statistics?.SampleCount ?? 0).ToString());
        }
#else
        public override bool TryGetSnapshot(
            out InputLatencySnapshot snapshot)
        {
            snapshot = new InputLatencySnapshot
            {
                IsAvailable = false,
                Status = "Input Latency requires ENABLE_DEBUG."
            };
            return true;
        }

        public override bool TryGetEvents(
            out InputLatencySampleEvent[] events,
            out int droppedEventCount)
        {
            events = Array.Empty<InputLatencySampleEvent>();
            droppedEventCount = 0;
            return false;
        }

        public RemoteMiniToolField[] CaptureFields()
        {
            return new[]
            {
                CreateField(
                    "status",
                    "Status",
                    "Requires ENABLE_DEBUG")
            };
        }
#endif
    }
}

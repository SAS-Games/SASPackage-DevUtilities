using System;
using System.Collections.Generic;
using HP.Utilities.DeveloperConsole.InputVisualizers;

namespace HP.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    internal abstract class RuntimeInputVisualizerMiniToolProvider : MiniToolStreamingDataProvider<InputVisualizerSnapshot, InputVisualizerSampleEvent>
    {
        private const int MaximumPendingEvents = 256;

        private readonly InputVisualizerSnapshotCollector _collector;
        private readonly List<InputVisualizerSampleEvent> _pendingEvents = new();
        private int _droppedEventCount;

        protected RuntimeInputVisualizerMiniToolProvider(InputVisualizerDeviceKind deviceKind)
        {
            _collector = new InputVisualizerSnapshotCollector(deviceKind);
        }

        public override void Start()
        {
            _collector.Reset();
            _pendingEvents.Clear();
            _droppedEventCount = 0;
        }

        public override void Stop()
        {
            _collector.Reset();
            _pendingEvents.Clear();
            _droppedEventCount = 0;
        }

        public override void Tick()
        {
            if (!_collector.TryCaptureChanges(out InputVisualizerSampleEvent sampleEvent))
                return;

            if (_pendingEvents.Count >= MaximumPendingEvents)
            {
                _pendingEvents.RemoveAt(0);
                _droppedEventCount++;
            }
            _pendingEvents.Add(sampleEvent);
        }

        public override bool TryGetSnapshot(out InputVisualizerSnapshot snapshot)
        {
            snapshot = _collector.Capture();
            return true;
        }

        public override bool TryGetEvents(out InputVisualizerSampleEvent[] events, out int droppedEventCount)
        {
            if (_pendingEvents.Count == 0)
            {
                events = Array.Empty<InputVisualizerSampleEvent>();
                droppedEventCount = 0;
                return false;
            }

            events = _pendingEvents.ToArray();
            droppedEventCount = _droppedEventCount;
            _pendingEvents.Clear();
            _droppedEventCount = 0;
            return true;
        }
    }
}

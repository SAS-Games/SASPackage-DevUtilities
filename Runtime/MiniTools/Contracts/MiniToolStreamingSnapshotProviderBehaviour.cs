using System;

namespace HP.DevUtilities
{
    /// <summary>
    /// Reusable local provider base for mini-tools that publish both a
    /// recoverable snapshot and incremental event batches.
    /// </summary>
    public abstract class MiniToolStreamingSnapshotProviderBehaviour<TSnapshot, TEvent> : MiniToolSnapshotProviderBehaviour<TSnapshot>, IMiniToolStreamProvider<TEvent> where TSnapshot : IMiniToolSnapshot where TEvent : IMiniToolStreamEvent
    {
        public event Action<TEvent[], int> EventsChanged;

        public abstract bool TryGetEvents(out TEvent[] events, out int droppedEventCount);

        protected void PublishEvents(TEvent[] events, int droppedEventCount)
        {
            if (events == null || events.Length == 0)
                return;

            EventsChanged?.Invoke(events, Math.Max(0, droppedEventCount));
        }
    }
}

namespace HP.DevUtilities
{
    /// <summary>
    /// Supplies bounded batches of changes that occurred after the previous
    /// capture. A snapshot provider should accompany a stream so a host can
    /// recover after connecting or missing a batch.
    /// </summary>
    public interface IMiniToolStreamProvider<TEvent> where TEvent : IMiniToolStreamEvent
    {
        bool TryGetEvents(out TEvent[] events, out int droppedEventCount);
    }
}

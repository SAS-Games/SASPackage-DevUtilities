namespace SAS.DevUtilities
{
    /// <summary>
    /// Consumes incremental mini-tool updates independently of the rendering
    /// technology used by the presentation.
    /// </summary>
    public interface IMiniToolStreamView<TEvent> where TEvent : IMiniToolStreamEvent
    {
        void ApplyEvents(TEvent[] events, int droppedEventCount);
    }
}

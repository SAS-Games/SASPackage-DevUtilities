namespace HP.DevUtilities
{
    /// <summary>
    /// Marks the controller that connects a mini-tool's local Player data
    /// source to its shared presentation.
    /// </summary>
    /// <remarks>
    /// The Editor Debug Host disables these controllers before applying a
    /// remote snapshot directly to the same presentation.
    /// </remarks>
    public interface IMiniToolLocalController
    {
    }
}

namespace HP.Utilities.Presentation
{
    /// <summary>
    /// A Dev Utilities presentation that preserves requested visibility while
    /// one or more optional systems temporarily suppress local UI.
    /// </summary>
    public interface IDevUtilityPresentation
    {
        void SetRequestedVisible(bool visible);
        void SetSuppressed(bool suppressed);
    }
}

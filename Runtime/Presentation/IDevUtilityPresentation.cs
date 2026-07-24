namespace SAS.Utilities.Presentation
{
    /// <summary>
    /// A Dev Utilities presentation that preserves requested visibility while
    /// one or more optional systems temporarily suppress local UI.
    /// </summary>
    public interface IDevUtilityPresentation : IDevUtilityUiVisibility
    {
        void SetSuppressed(bool suppressed);
    }
}

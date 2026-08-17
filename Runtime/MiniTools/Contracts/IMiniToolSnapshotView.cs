namespace HP.DevUtilities
{
    /// <summary>
    /// Applies a recoverable snapshot to a mini-tool presentation.
    /// Snapshot views are intentionally separate from stream views because a
    /// snapshot replaces current data while a stream appends ordered changes.
    /// </summary>
    public interface IMiniToolSnapshotView<TSnapshot> where TSnapshot : IMiniToolSnapshot
    {
        void ApplySnapshot(in TSnapshot snapshot);
    }
}

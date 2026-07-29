namespace SAS.DevUtilities
{
    /// <summary>
    /// Supplies recoverable snapshots to a mini-tool presentation. The
    /// registered MiniToolDefinition, not the provider, owns the identity.
    /// </summary>
    public interface IMiniToolSnapshotProvider<TSnapshot> where TSnapshot : IMiniToolSnapshot
    {
        bool TryGetSnapshot(out TSnapshot snapshot);
    }
}

using System;

namespace SAS.DevUtilities
{
    /// <summary>
    /// Legacy mini-tool identity contract.
    /// </summary>
    /// <remarks>
    /// Mini-tool identity and display metadata are now owned by the registered
    /// MiniToolDefinition. Snapshot providers and views should implement only
    /// IMiniToolSnapshotProvider&lt;TSnapshot&gt; and
    /// IMiniToolSnapshotView&lt;TSnapshot&gt;.
    /// </remarks>
    [Obsolete("Mini-tool identity is owned by MiniToolDefinition. " + "Implement IMiniToolSnapshotProvider<TSnapshot> and/or " + "IMiniToolSnapshotView<TSnapshot> without duplicating an ID.")]
    public interface IMiniTool
    {
        string ToolId { get; }

        string DisplayName { get; }
    }
}

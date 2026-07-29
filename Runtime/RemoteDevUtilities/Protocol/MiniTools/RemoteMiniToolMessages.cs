using System;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools
{
    [Flags]
    public enum RemoteMiniToolCapabilities
    {
        None = 0,
        NativeWorkspaceFields = 1 << 0,
        TypedDebugHostSnapshot = 1 << 1,
        EventStream = 1 << 2,
        Actions = 1 << 3
    }

    [Serializable]
    public sealed class RemoteMiniToolCatalogRequest
    {
    }

    [Serializable]
    public sealed class RemoteMiniToolCatalogResponse
    {
        public RemoteMiniToolDescriptor[] Tools = Array.Empty<RemoteMiniToolDescriptor>();
    }

    [Serializable]
    public sealed class RemoteMiniToolDescriptor
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public float DefaultIntervalSeconds;
        public float DefaultStreamIntervalSeconds;
        public bool VisibleByDefault = true;
        public RemoteMiniToolCapabilities Capabilities;
        public RemoteMiniToolCommandManifest Command;
        public RemoteMiniToolActionDescriptor[] Actions =
            Array.Empty<RemoteMiniToolActionDescriptor>();
    }

    /// <summary>
    /// Portable command metadata supplied by the mini-tool provider. It travels with the
    /// Player catalog, allowing an Editor with no project-specific setup to route the command.
    /// </summary>
    [Serializable]
    public sealed class RemoteMiniToolCommandManifest
    {
        public string Name;
        public RemoteCommandRouting SuggestedRouting =
            RemoteCommandRouting.ControlEditorToolOnly;
    }

    [Serializable]
    public sealed class RemoteMiniToolSubscriptionRequest
    {
        public string ToolId;
        public bool Subscribe;
        public float IntervalSeconds;
        public float StreamIntervalSeconds;
    }

    [Serializable]
    public sealed class RemoteMiniToolSubscriptionResponse
    {
        public string ToolId;
        public bool Success;
        public bool Subscribed;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteMiniToolActionDescriptor
    {
        public string Id;
        public string DisplayName;
        public bool HideInNativeWorkspace;
    }

    [Serializable]
    public sealed class RemoteMiniToolActionRequest
    {
        public string ToolId;
        public string ActionId;
    }

    [Serializable]
    public sealed class RemoteMiniToolActionResponse
    {
        public string ToolId;
        public string ActionId;
        public bool Success;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteMiniToolSample
    {
        public string ToolId;
        public double Timestamp;
        public int Frame;

        /// <summary>
        /// Assembly-qualified name of the <see cref="SAS.DevUtilities.IMiniToolSnapshot"/>
        /// carried by <see cref="SnapshotJson"/>. Empty for field-only providers.
        /// </summary>
        public string SnapshotTypeName;

        /// <summary>
        /// Serialized mini-tool snapshot consumed by the matching
        /// <see cref="SAS.DevUtilities.IMiniToolSnapshotView{TSnapshot}"/> on the Debug Host prefab.
        /// </summary>
        public string SnapshotJson;

        public RemoteMiniToolField[] Fields = Array.Empty<RemoteMiniToolField>();
    }

    [Serializable]
    public sealed class RemoteMiniToolStreamBatch
    {
        public string ToolId;
        public double Timestamp;
        public int Frame;
        public long Sequence;
        public int DroppedEventCount;
        public string EventTypeName;
        public string EventsJson;
    }

    /// <summary>
    /// JSON wrapper used because Unity's JsonUtility does not support a root
    /// array.
    /// </summary>
    [Serializable]
    public sealed class RemoteMiniToolStreamPayload<TEvent>
        where TEvent : SAS.DevUtilities.IMiniToolStreamEvent
    {
        public TEvent[] Events = Array.Empty<TEvent>();
    }

    [Serializable]
    public sealed class RemoteMiniToolField
    {
        public string Name;
        public string DisplayName;
        public string Value;
        public string Unit;
    }
}

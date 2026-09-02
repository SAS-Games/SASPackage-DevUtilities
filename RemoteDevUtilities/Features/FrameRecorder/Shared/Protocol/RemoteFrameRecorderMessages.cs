using System;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;

namespace SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder
{
    public static class RemoteFrameRecorderLimits
    {
        public const int MinimumCapacity = 1;
        public const int MaximumCapacity = 300;
        public const int DefaultCapacity = 30;
        public const int TransferChunkBytes = 512 * 1024;
        public const int MaximumFrameTransferBytes = 64 * 1024 * 1024;
    }

    public static class RemoteRecordedSceneGraphFormats
    {
        public const int LegacyFullSnapshot = 0;
        public const int ContentAddressedSections = 1;
        public const int ContentAddressedObjects = 2;
    }

    public static class RemoteFrameRecorderMessageTypes
    {
        public const string ControlRequest = "frame-recorder.control.request";
        public const string ControlResponse = "frame-recorder.control.response";
        public const string ManifestRequest = "frame-recorder.manifest.request";
        public const string ManifestResponse = "frame-recorder.manifest.response";
        public const string FrameRequest = "frame-recorder.frame.request";
        public const string FrameResponse = "frame-recorder.frame.response";
        public const string FrameChunkRequest = "frame-recorder.frame-chunk.request";
        public const string FrameChunkResponse = "frame-recorder.frame-chunk.response";
    }

    public enum RemoteFrameRecorderAction
    {
        Query,
        Start,
        Seal,
        Release
    }

    public enum RemoteFrameRecorderState
    {
        Idle,
        Recording,
        Finalizing,
        Sealed
    }

    public enum RemoteFrameRecorderInspectorScope
    {
        HierarchyOnly,
        SelectedObject,
        AllObjects
    }

    [Serializable]
    public sealed class RemoteFrameRecorderControlRequest
    {
        public RemoteFrameRecorderAction Action;
        public int Capacity = RemoteFrameRecorderLimits.DefaultCapacity;
        public int MaximumWidth = 640;
        public int JpegQuality = 60;
        public bool FreezePlayerWhenSealed;
        public RemoteFrameRecorderInspectorScope InspectorScope = RemoteFrameRecorderInspectorScope.SelectedObject;
        public long InspectedObjectId;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderControlResponse
    {
        public RemoteFrameRecorderAction Action;
        public RemoteFrameRecorderState State;
        public long RecordingId;
        public int Capacity;
        public int CapturedFrameCount;
        public int PendingFrameCount;
        public int MissedFrameCount;
        public int FirstUnityFrame;
        public int LastUnityFrame;
        public long StoredBytes;
        public long SceneGraphBytesSaved;
        public bool UsesAsyncGpuReadback;
        public bool PlayerFrozen;
        public RemoteFrameRecorderInspectorScope InspectorScope;
        public long InspectedObjectId;
        public string Warning;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderManifestRequest
    {
        public long RecordingId;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderManifestResponse
    {
        public long RecordingId;
        public RemoteFrameRecorderState State;
        public RemoteRecordedFrameInfo[] Frames = Array.Empty<RemoteRecordedFrameInfo>();
        public string Error;
    }

    [Serializable]
    public sealed class RemoteRecordedFrameInfo
    {
        public int UnityFrame;
        public double RealtimeSeconds;
        public int Width;
        public int Height;
        public int ImageBytes;
        public int SceneGraphBytes;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderFrameRequest
    {
        public long RecordingId;
        public int UnityFrame;
        public int SupportedSceneGraphFormatVersion;
        public bool SupportsChunkedTransfer;
        public string KnownHierarchySnapshotId;
        public string KnownInspectorSnapshotId;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderFrameResponse
    {
        public long RecordingId;
        public int UnityFrame;
        public string ImageBase64;
        public int SceneGraphFormatVersion;
        public string HierarchySnapshotId;
        public string HierarchyGzipBase64;
        public string InspectorSnapshotId;
        public string InspectorGzipBase64;
        public string InspectorManifestGzipBase64;
        public RemoteRecordedSceneGraphBlob[] InspectorBlobs = Array.Empty<RemoteRecordedSceneGraphBlob>();
        public string ChunkTransferId;
        public int ChunkTransferBytes;

        public string ChunkTransferSha256;

        // Retained so the current Editor can still read recordings produced by older Players.
        public string SceneGraphGzipBase64;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderFrameChunkRequest
    {
        public long RecordingId;
        public int UnityFrame;
        public string TransferId;
        public int Offset;
    }

    [Serializable]
    public sealed class RemoteFrameRecorderFrameChunkResponse
    {
        public long RecordingId;
        public int UnityFrame;
        public string TransferId;
        public int Offset;
        public int TotalBytes;
        public string DataBase64;
        public bool IsLast;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteRecordedSceneGraph
    {
        public RemoteSceneInspectorHierarchyResponse Hierarchy = new();
        public RemoteObjectDetails[] Inspections = Array.Empty<RemoteObjectDetails>();
        public string Error;
    }

    [Serializable]
    public sealed class RemoteRecordedInspectorSnapshot
    {
        public RemoteObjectDetails[] Inspections = Array.Empty<RemoteObjectDetails>();
        public string Error;
    }

    [Serializable]
    public sealed class RemoteRecordedInspectorManifest
    {
        public RemoteRecordedObjectSnapshotReference[] Objects = Array.Empty<RemoteRecordedObjectSnapshotReference>();
        public string Error;
    }

    [Serializable]
    public sealed class RemoteRecordedObjectSnapshotReference
    {
        public long ObjectId;
        public bool IsNull;
        public string HeaderSnapshotId;
        public string MaterialSnapshotId;
        public string[] ComponentSnapshotIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RemoteRecordedObjectHeader
    {
        public long Id;
        public string Name;
        public bool Active;
        public bool ActiveReadOnly;
        public string Tag;
        public int Layer;
        public bool LayerReadOnly;
    }

    [Serializable]
    public sealed class RemoteRecordedMaterialSnapshot
    {
        public RemoteMaterialShaderSection MaterialsAndShaders;
    }

    [Serializable]
    public sealed class RemoteRecordedSceneGraphBlob
    {
        public string SnapshotId;
        public string GzipBase64;
    }
}

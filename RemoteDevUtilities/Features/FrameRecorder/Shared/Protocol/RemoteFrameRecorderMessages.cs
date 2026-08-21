using System;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;

namespace SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder
{
    public static class RemoteFrameRecorderLimits
    {
        public const int MinimumCapacity = 1;
        public const int MaximumCapacity = 300;
        public const int DefaultCapacity = 30;
    }

    public static class RemoteFrameRecorderMessageTypes
    {
        public const string ControlRequest = "frame-recorder.control.request";
        public const string ControlResponse = "frame-recorder.control.response";
        public const string ManifestRequest = "frame-recorder.manifest.request";
        public const string ManifestResponse = "frame-recorder.manifest.response";
        public const string FrameRequest = "frame-recorder.frame.request";
        public const string FrameResponse = "frame-recorder.frame.response";
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
        public RemoteFrameRecorderInspectorScope InspectorScope =
            RemoteFrameRecorderInspectorScope.SelectedObject;
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
    }

    [Serializable]
    public sealed class RemoteFrameRecorderFrameResponse
    {
        public long RecordingId;
        public int UnityFrame;
        public string ImageBase64;
        public string SceneGraphGzipBase64;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteRecordedSceneGraph
    {
        public RemoteSceneInspectorHierarchyResponse Hierarchy = new();
        public RemoteObjectDetails[] Inspections = Array.Empty<RemoteObjectDetails>();
        public string Error;
    }
}

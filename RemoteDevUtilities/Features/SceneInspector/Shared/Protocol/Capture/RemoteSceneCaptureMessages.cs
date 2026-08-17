using System;

namespace SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.Capture
{
    [Serializable]
    public sealed class RemoteSceneCaptureRequest
    {
        public int MaximumWidth = 960;
        public int JpegQuality = 70;
        public bool FreezeWhilePicking = true;
    }

    [Serializable]
    public sealed class RemoteSceneCaptureResponse
    {
        public long CaptureId;
        public string ImageBase64;
        public int Width;
        public int Height;
        public int FrameCount;
        public bool PlayerFrozen;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteScenePickRequest
    {
        public long CaptureId;
        public float NormalizedX;
        public float NormalizedY;
        public bool Cancel;
    }

    [Serializable]
    public sealed class RemoteScenePickResponse
    {
        public long CaptureId;
        public bool Found;
        public long ObjectId;
        public RemoteScenePickCandidate[] Candidates = Array.Empty<RemoteScenePickCandidate>();
        public bool Cancelled;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteScenePickCandidate
    {
        public long ObjectId;
        public string Name;
        public string HierarchyPath;
        public string Source;
    }
}

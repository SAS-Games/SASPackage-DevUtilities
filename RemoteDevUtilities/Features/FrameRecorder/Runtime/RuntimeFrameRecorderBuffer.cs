using System;
using System.Collections.Generic;
using System.Linq;
using SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder;

namespace SAS.Utilities.RemoteDevUtilities.FrameRecorder
{
    internal sealed class RuntimeRecordedFrameData
    {
        internal long RecordingId;
        internal int UnityFrame;
        internal double RealtimeSeconds;
        internal int Width;
        internal int Height;
        internal byte[] JpegBytes;
        internal byte[] SceneGraphGzipBytes;

        internal long StoredBytes => (JpegBytes?.LongLength ?? 0L) + (SceneGraphGzipBytes?.LongLength ?? 0L);

        internal RemoteRecordedFrameInfo ToInfo() => new()
        {
            UnityFrame = UnityFrame,
            RealtimeSeconds = RealtimeSeconds,
            Width = Width,
            Height = Height,
            ImageBytes = JpegBytes?.Length ?? 0,
            SceneGraphBytes = SceneGraphGzipBytes?.Length ?? 0
        };
    }

    internal sealed class RuntimeFrameRecorderBuffer
    {
        private readonly object _gate = new();
        private readonly SortedDictionary<int, RuntimeRecordedFrameData> _frames = new();
        private long _recordingId;
        private int _capacity;

        internal long RecordingId
        {
            get { lock (_gate) return _recordingId; }
        }

        internal int Capacity
        {
            get { lock (_gate) return _capacity; }
        }

        internal int Count
        {
            get { lock (_gate) return _frames.Count; }
        }

        internal long StoredBytes
        {
            get
            {
                lock (_gate)
                    return _frames.Values.Sum(frame => frame.StoredBytes);
            }
        }

        internal void Reset(long recordingId, int capacity)
        {
            lock (_gate)
            {
                _recordingId = recordingId;
                _capacity = Math.Max(1, capacity);
                _frames.Clear();
            }
        }

        internal void Clear(long recordingId = 0)
        {
            lock (_gate)
            {
                if (recordingId != 0 && recordingId != _recordingId)
                    return;
                _recordingId = 0;
                _capacity = 0;
                _frames.Clear();
            }
        }

        internal bool Add(RuntimeRecordedFrameData frame)
        {
            if (frame == null)
                return false;

            lock (_gate)
            {
                if (_recordingId == 0 || frame.RecordingId != _recordingId)
                    return false;

                _frames[frame.UnityFrame] = frame;
                while (_frames.Count > _capacity)
                    _frames.Remove(_frames.Keys.First());
                return true;
            }
        }

        internal bool TryGet(int unityFrame, out RuntimeRecordedFrameData frame)
        {
            lock (_gate)
                return _frames.TryGetValue(unityFrame, out frame);
        }

        internal RemoteRecordedFrameInfo[] GetManifest()
        {
            lock (_gate)
                return _frames.Values.Select(frame => frame.ToInfo()).ToArray();
        }
    }
}

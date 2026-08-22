using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Editor.FrameRecorder
{
    internal sealed class RemoteFrameReplayFrame
    {
        internal RemoteRecordedFrameInfo Info;
        internal string ImageBase64;
        internal RemoteRecordedSceneGraph SceneGraph;
    }

    [RemoteEditorFeature("frame-recorder", 410)]
    internal sealed class RemoteFrameRecorderClient : IRemoteEditorFeatureClient
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteFrameRecorderMessageTypes.ControlResponse,
            RemoteFrameRecorderMessageTypes.ManifestResponse,
            RemoteFrameRecorderMessageTypes.FrameResponse
        };

        private readonly IRemoteEditorSession _session;
        private readonly List<RemoteFrameReplayFrame> _replayFrames = new();
        private long _controlRequestId;
        private long _manifestRequestId;
        private long _frameRequestId;
        private int _nextDownloadIndex;
        private string _knownHierarchySnapshotId;
        private string _knownInspectorSnapshotId;
        private RemoteSceneInspectorHierarchyResponse _knownHierarchy;
        private RemoteRecordedInspectorSnapshot _knownInspector;

        public RemoteFrameRecorderClient(IRemoteEditorSession session) => _session = session;

        public IEnumerable<string> MessageTypes => SupportedMessages;
        internal RemoteFrameRecorderControlResponse Status { get; private set; } = new();
        internal RemoteFrameRecorderManifestResponse Manifest { get; private set; }
        internal IReadOnlyList<RemoteFrameReplayFrame> ReplayFrames => _replayFrames;
        internal string DownloadError { get; private set; }
        internal int SessionGeneration { get; private set; }
        internal bool IsControlPending => _controlRequestId != 0;
        internal bool IsDownloading => _manifestRequestId != 0 || _frameRequestId != 0;
        internal int DownloadedFrameCount => _replayFrames.Count;
        internal int DownloadFrameCount => Manifest?.Frames?.Length ?? 0;

        public void OnConnected() => Query();

        internal void Query()
        {
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest,
                new RemoteFrameRecorderControlRequest { Action = RemoteFrameRecorderAction.Query });
        }

        internal void Start(int capacity, int maximumWidth, int jpegQuality,
            RemoteFrameRecorderInspectorScope inspectorScope, long inspectedObjectId)
        {
            ClearReplay();
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest,
                new RemoteFrameRecorderControlRequest
                {
                    Action = RemoteFrameRecorderAction.Start,
                    Capacity = capacity,
                    MaximumWidth = maximumWidth,
                    JpegQuality = jpegQuality,
                    InspectorScope = inspectorScope,
                    InspectedObjectId = inspectedObjectId
                });
        }

        internal void SealAndFetch(bool freezePlayer)
        {
            if (Status.RecordingId == 0)
                return;
            ClearReplay();
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest,
                new RemoteFrameRecorderControlRequest
                {
                    Action = RemoteFrameRecorderAction.Seal,
                    FreezePlayerWhenSealed = freezePlayer
                });
        }

        internal void Release()
        {
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest,
                new RemoteFrameRecorderControlRequest { Action = RemoteFrameRecorderAction.Release });
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteFrameRecorderMessageTypes.ControlResponse:
                    HandleControl(envelope);
                    break;
                case RemoteFrameRecorderMessageTypes.ManifestResponse:
                    HandleManifest(envelope);
                    break;
                case RemoteFrameRecorderMessageTypes.FrameResponse:
                    HandleFrame(envelope);
                    break;
            }
            _session.NotifyStateChanged();
        }

        public void Reset()
        {
            SessionGeneration++;
            _controlRequestId = 0;
            _manifestRequestId = 0;
            _frameRequestId = 0;
            Status = new RemoteFrameRecorderControlResponse();
            ClearReplay();
        }

        private void HandleControl(RemoteEnvelope envelope)
        {
            if (envelope.RequestId != _controlRequestId)
                return;
            _controlRequestId = 0;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                    out RemoteFrameRecorderControlResponse response, out string error))
            {
                DownloadError = error;
                return;
            }

            Status = response;
            if (!string.IsNullOrEmpty(response.Error) && response.Action != RemoteFrameRecorderAction.Query)
            {
                DownloadError = response.Error;
                return;
            }

            if (response.Action == RemoteFrameRecorderAction.Seal &&
                response.State == RemoteFrameRecorderState.Sealed && response.RecordingId != 0)
                RequestManifest(response.RecordingId);
        }

        private void RequestManifest(long recordingId)
        {
            _manifestRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ManifestRequest,
                new RemoteFrameRecorderManifestRequest { RecordingId = recordingId });
        }

        private void HandleManifest(RemoteEnvelope envelope)
        {
            if (envelope.RequestId != _manifestRequestId)
                return;
            _manifestRequestId = 0;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                    out RemoteFrameRecorderManifestResponse response, out string error))
            {
                DownloadError = error;
                return;
            }

            Manifest = response;
            if (!string.IsNullOrEmpty(response.Error))
            {
                DownloadError = response.Error;
                return;
            }

            _nextDownloadIndex = 0;
            RequestNextFrame();
        }

        private void RequestNextFrame()
        {
            RemoteRecordedFrameInfo[] frames = Manifest?.Frames;
            if (frames == null || _nextDownloadIndex >= frames.Length)
            {
                _frameRequestId = 0;
                return;
            }

            _frameRequestId = _session.Send(RemoteFrameRecorderMessageTypes.FrameRequest,
                new RemoteFrameRecorderFrameRequest
                {
                    RecordingId = Manifest.RecordingId,
                    UnityFrame = frames[_nextDownloadIndex].UnityFrame,
                    SupportedSceneGraphFormatVersion =
                        RemoteRecordedSceneGraphFormats.ContentAddressedSections,
                    KnownHierarchySnapshotId = _knownHierarchySnapshotId,
                    KnownInspectorSnapshotId = _knownInspectorSnapshotId
                });
        }

        private void HandleFrame(RemoteEnvelope envelope)
        {
            if (envelope.RequestId != _frameRequestId)
                return;
            _frameRequestId = 0;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                    out RemoteFrameRecorderFrameResponse response, out string error))
            {
                DownloadError = error;
                return;
            }

            if (!string.IsNullOrEmpty(response.Error))
            {
                DownloadError = response.Error;
                return;
            }

            try
            {
                RemoteRecordedSceneGraph graph = response.SceneGraphFormatVersion >=
                                                 RemoteRecordedSceneGraphFormats.ContentAddressedSections
                    ? ResolveSectionedSceneGraph(response)
                    : ResolveLegacySceneGraph(response);
                RemoteRecordedFrameInfo info = Manifest.Frames[_nextDownloadIndex];
                _replayFrames.Add(new RemoteFrameReplayFrame
                {
                    Info = info,
                    ImageBase64 = response.ImageBase64,
                    SceneGraph = graph ?? new RemoteRecordedSceneGraph()
                });
                _nextDownloadIndex++;
                RequestNextFrame();
            }
            catch (Exception exception)
            {
                DownloadError = exception.GetType().Name + ": " + exception.Message;
            }
        }

        private void ClearReplay()
        {
            Manifest = null;
            DownloadError = null;
            _nextDownloadIndex = 0;
            _manifestRequestId = 0;
            _frameRequestId = 0;
            _replayFrames.Clear();
            _knownHierarchySnapshotId = null;
            _knownInspectorSnapshotId = null;
            _knownHierarchy = null;
            _knownInspector = null;
        }

        private RemoteRecordedSceneGraph ResolveSectionedSceneGraph(
            RemoteFrameRecorderFrameResponse response)
        {
            if (string.IsNullOrEmpty(response.HierarchySnapshotId) ||
                string.IsNullOrEmpty(response.InspectorSnapshotId))
                throw new InvalidDataException("The recorded frame has incomplete scene-graph references.");

            if (!string.IsNullOrEmpty(response.HierarchyGzipBase64))
            {
                _knownHierarchy = DeserializeCompressed<RemoteSceneInspectorHierarchyResponse>(
                    response.HierarchyGzipBase64);
                _knownHierarchySnapshotId = response.HierarchySnapshotId;
            }
            else if (!string.Equals(_knownHierarchySnapshotId, response.HierarchySnapshotId,
                         StringComparison.Ordinal) || _knownHierarchy == null)
            {
                throw new InvalidDataException("The hierarchy delta references an unavailable snapshot.");
            }

            if (!string.IsNullOrEmpty(response.InspectorGzipBase64))
            {
                _knownInspector = DeserializeCompressed<RemoteRecordedInspectorSnapshot>(
                    response.InspectorGzipBase64);
                _knownInspectorSnapshotId = response.InspectorSnapshotId;
            }
            else if (!string.Equals(_knownInspectorSnapshotId, response.InspectorSnapshotId,
                         StringComparison.Ordinal) || _knownInspector == null)
            {
                throw new InvalidDataException("The inspector delta references an unavailable snapshot.");
            }

            return new RemoteRecordedSceneGraph
            {
                Hierarchy = _knownHierarchy ?? new RemoteSceneInspectorHierarchyResponse(),
                Inspections = _knownInspector?.Inspections ?? Array.Empty<RemoteObjectDetails>(),
                Error = _knownInspector?.Error
            };
        }

        private static RemoteRecordedSceneGraph ResolveLegacySceneGraph(
            RemoteFrameRecorderFrameResponse response)
        {
            return DeserializeCompressed<RemoteRecordedSceneGraph>(response.SceneGraphGzipBase64) ??
                   new RemoteRecordedSceneGraph();
        }

        private static T DeserializeCompressed<T>(string base64)
        {
            byte[] compressed = Convert.FromBase64String(base64 ?? string.Empty);
            string json = Encoding.UTF8.GetString(Decompress(compressed));
            return JsonUtility.FromJson<T>(json);
        }

        private static byte[] Decompress(byte[] compressed)
        {
            using var input = new MemoryStream(compressed ?? Array.Empty<byte>());
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }
    }
}

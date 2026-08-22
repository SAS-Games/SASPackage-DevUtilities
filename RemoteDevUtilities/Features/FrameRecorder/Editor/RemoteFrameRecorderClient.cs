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

    [RemoteEditorFeature("frame-recorder", 410, experimental: true)]
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
        private RemoteRecordedInspectorManifest _knownInspectorManifest;
        private readonly Dictionary<string, string> _knownInspectorBlobs =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _decodedInspectorBlobs =
            new(StringComparer.Ordinal);
        private Dictionary<long, CachedRemoteObject> _knownInspectorObjects = new();

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
                        RemoteRecordedSceneGraphFormats.ContentAddressedObjects,
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
                RemoteRecordedSceneGraph graph;
                if (response.SceneGraphFormatVersion >=
                    RemoteRecordedSceneGraphFormats.ContentAddressedObjects)
                    graph = ResolveObjectSectionedSceneGraph(response);
                else if (response.SceneGraphFormatVersion >=
                         RemoteRecordedSceneGraphFormats.ContentAddressedSections)
                    graph = ResolveSectionedSceneGraph(response);
                else
                    graph = ResolveLegacySceneGraph(response);
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
            _knownInspectorManifest = null;
            _knownInspectorBlobs.Clear();
            _decodedInspectorBlobs.Clear();
            _knownInspectorObjects.Clear();
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

        private RemoteRecordedSceneGraph ResolveObjectSectionedSceneGraph(
            RemoteFrameRecorderFrameResponse response)
        {
            ResolveHierarchy(response);
            if (string.IsNullOrEmpty(response.InspectorSnapshotId))
                throw new InvalidDataException("The recorded frame has no inspector manifest reference.");

            if (!string.IsNullOrEmpty(response.InspectorManifestGzipBase64))
            {
                _knownInspectorManifest =
                    DeserializeCompressed<RemoteRecordedInspectorManifest>(
                        response.InspectorManifestGzipBase64);
                _knownInspectorSnapshotId = response.InspectorSnapshotId;
            }
            else if (!string.Equals(_knownInspectorSnapshotId, response.InspectorSnapshotId,
                         StringComparison.Ordinal) || _knownInspectorManifest == null)
            {
                throw new InvalidDataException(
                    "The inspector manifest references an unavailable snapshot.");
            }

            RemoteRecordedSceneGraphBlob[] suppliedBlobs = response.InspectorBlobs ??
                                                           Array.Empty<RemoteRecordedSceneGraphBlob>();
            for (int i = 0; i < suppliedBlobs.Length; i++)
            {
                RemoteRecordedSceneGraphBlob blob = suppliedBlobs[i];
                if (blob == null || string.IsNullOrEmpty(blob.SnapshotId) ||
                    string.IsNullOrEmpty(blob.GzipBase64))
                    throw new InvalidDataException("The inspector response contains an invalid object snapshot.");
                _knownInspectorBlobs[blob.SnapshotId] = blob.GzipBase64;
                _decodedInspectorBlobs.Remove(blob.SnapshotId);
            }

            RemoteRecordedObjectSnapshotReference[] references =
                _knownInspectorManifest.Objects ??
                Array.Empty<RemoteRecordedObjectSnapshotReference>();
            var inspections = new RemoteObjectDetails[references.Length];
            var nextObjects = new Dictionary<long, CachedRemoteObject>();
            var requiredPayloads = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < references.Length; i++)
            {
                RemoteRecordedObjectSnapshotReference reference = references[i];
                if (reference == null || reference.IsNull)
                    continue;
                AddRequiredPayloads(reference, requiredPayloads);
                if (_knownInspectorObjects.TryGetValue(reference.ObjectId,
                        out CachedRemoteObject cached) && cached.Matches(reference))
                {
                    inspections[i] = cached.Details;
                    nextObjects[reference.ObjectId] = cached;
                    continue;
                }

                RemoteRecordedObjectHeader header = ReadInspectorBlob<RemoteRecordedObjectHeader>(
                    reference.HeaderSnapshotId);
                if (header == null || header.Id != reference.ObjectId)
                    throw new InvalidDataException("A recorded object header is invalid.");
                RemoteRecordedMaterialSnapshot material =
                    ReadInspectorBlob<RemoteRecordedMaterialSnapshot>(
                        reference.MaterialSnapshotId);
                string[] componentIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
                var components = new RemoteComponentDescriptor[componentIds.Length];
                for (int componentIndex = 0; componentIndex < componentIds.Length;
                     componentIndex++)
                {
                    components[componentIndex] = ReadInspectorBlob<RemoteComponentDescriptor>(
                        componentIds[componentIndex]);
                }

                var details = new RemoteObjectDetails
                {
                    Id = header.Id,
                    Name = header.Name,
                    Active = header.Active,
                    ActiveReadOnly = header.ActiveReadOnly,
                    Tag = header.Tag,
                    Layer = header.Layer,
                    LayerReadOnly = header.LayerReadOnly,
                    Components = components,
                    MaterialsAndShaders = material?.MaterialsAndShaders
                };
                inspections[i] = details;
                nextObjects[reference.ObjectId] = new CachedRemoteObject(reference, details);
            }

            PruneInspectorBlobs(requiredPayloads);
            _knownInspectorObjects = nextObjects;
            return new RemoteRecordedSceneGraph
            {
                Hierarchy = _knownHierarchy ?? new RemoteSceneInspectorHierarchyResponse(),
                Inspections = inspections,
                Error = _knownInspectorManifest.Error
            };
        }

        private void ResolveHierarchy(RemoteFrameRecorderFrameResponse response)
        {
            if (string.IsNullOrEmpty(response.HierarchySnapshotId))
                throw new InvalidDataException("The recorded frame has no hierarchy reference.");
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
        }

        private T ReadInspectorBlob<T>(string snapshotId) where T : class
        {
            if (string.IsNullOrEmpty(snapshotId) ||
                !_knownInspectorBlobs.TryGetValue(snapshotId, out string base64))
                throw new InvalidDataException("An inspector object snapshot is unavailable.");
            if (_decodedInspectorBlobs.TryGetValue(snapshotId, out object decoded))
            {
                if (decoded is T typed)
                    return typed;
                throw new InvalidDataException("An inspector snapshot has an unexpected payload type.");
            }

            T value = DeserializeCompressed<T>(base64);
            if (value == null)
                throw new InvalidDataException("An inspector object snapshot could not be decoded.");
            _decodedInspectorBlobs[snapshotId] = value;
            return value;
        }

        private static void AddRequiredPayloads(RemoteRecordedObjectSnapshotReference reference,
            ISet<string> destination)
        {
            if (!string.IsNullOrEmpty(reference.HeaderSnapshotId))
                destination.Add(reference.HeaderSnapshotId);
            if (!string.IsNullOrEmpty(reference.MaterialSnapshotId))
                destination.Add(reference.MaterialSnapshotId);
            string[] componentIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
            for (int i = 0; i < componentIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(componentIds[i]))
                    destination.Add(componentIds[i]);
            }
        }

        private void PruneInspectorBlobs(ISet<string> requiredPayloads)
        {
            var obsolete = new List<string>();
            foreach (string snapshotId in _knownInspectorBlobs.Keys)
            {
                if (!requiredPayloads.Contains(snapshotId))
                    obsolete.Add(snapshotId);
            }
            for (int i = 0; i < obsolete.Count; i++)
            {
                _knownInspectorBlobs.Remove(obsolete[i]);
                _decodedInspectorBlobs.Remove(obsolete[i]);
            }
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

        private sealed class CachedRemoteObject
        {
            private readonly string _headerSnapshotId;
            private readonly string _materialSnapshotId;
            private readonly string[] _componentSnapshotIds;

            internal CachedRemoteObject(RemoteRecordedObjectSnapshotReference reference,
                RemoteObjectDetails details)
            {
                _headerSnapshotId = reference.HeaderSnapshotId;
                _materialSnapshotId = reference.MaterialSnapshotId;
                _componentSnapshotIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
                Details = details;
            }

            internal RemoteObjectDetails Details { get; }

            internal bool Matches(RemoteRecordedObjectSnapshotReference reference)
            {
                if (!string.Equals(_headerSnapshotId, reference.HeaderSnapshotId,
                        StringComparison.Ordinal) ||
                    !string.Equals(_materialSnapshotId, reference.MaterialSnapshotId,
                        StringComparison.Ordinal))
                    return false;
                string[] componentIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
                if (_componentSnapshotIds.Length != componentIds.Length)
                    return false;
                for (int i = 0; i < componentIds.Length; i++)
                {
                    if (!string.Equals(_componentSnapshotIds[i], componentIds[i],
                            StringComparison.Ordinal))
                        return false;
                }
                return true;
            }
        }
    }
}

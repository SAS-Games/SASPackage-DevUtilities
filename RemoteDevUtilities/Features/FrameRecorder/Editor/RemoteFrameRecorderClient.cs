using System;
using System.Collections.Generic;
using System.Globalization;
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
    [Serializable]
    internal sealed class RemoteFrameRecordingArchiveHeader
    {
        public int FormatVersion;
        public string CreatedUtc;
        public int FrameCount;
        public RemoteFrameRecorderManifestResponse Manifest;
    }

    internal sealed class RemoteFrameRecordingArchive
    {
        internal RemoteFrameRecorderManifestResponse Manifest;
        internal RemoteFrameRecorderFrameResponse[] FrameResponses = Array.Empty<RemoteFrameRecorderFrameResponse>();
    }

    internal static class RemoteFrameRecordingStore
    {
        internal const string FileExtension = "framerecording";
        private const int CurrentFormatVersion = 1;
        private const int MaximumJsonEntryBytes = 64 * 1024 * 1024;
        private const string HeaderEntryName = "recording.json";

        internal static void Save(string path, RemoteFrameRecorderManifestResponse manifest, IReadOnlyList<RemoteFrameReplayFrame> frames)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A recording file path is required.", nameof(path));
            if (manifest?.Frames == null || frames == null || frames.Count == 0)
                throw new InvalidOperationException("There are no downloaded frames to save.");
            if (manifest.Frames.Length != frames.Count)
                throw new InvalidDataException("The downloaded frame manifest is incomplete.");

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("The selected recording directory does not exist.");

            string temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
                {
                    WriteJson(archive, HeaderEntryName, new RemoteFrameRecordingArchiveHeader
                    {
                        FormatVersion = CurrentFormatVersion,
                        CreatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        FrameCount = frames.Count,
                        Manifest = manifest
                    });

                    for (int i = 0; i < frames.Count; i++)
                    {
                        RemoteFrameReplayFrame frame = frames[i];
                        RemoteRecordedFrameInfo info = manifest.Frames[i];
                        if (frame?.Info == null || info == null || frame.Info.UnityFrame != info.UnityFrame)
                            throw new InvalidDataException($"Downloaded frame {i + 1} does not match its manifest entry.");

                        RemoteFrameRecorderFrameResponse response = frame.SourceResponse ?? CreateLegacyResponse(manifest.RecordingId, frame);
                        ValidateResponse(response, manifest.RecordingId, info.UnityFrame, i);
                        WriteJson(archive, GetFrameEntryName(i), response);
                    }
                }

                ReplaceDestination(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        internal static RemoteFrameRecordingArchive Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A recording file path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("The selected frame recording does not exist.", fullPath);

            using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
            RemoteFrameRecordingArchiveHeader header = ReadJson<RemoteFrameRecordingArchiveHeader>(archive, HeaderEntryName);
            if (header == null || header.FormatVersion != CurrentFormatVersion)
                throw new InvalidDataException($"This frame recording uses an unsupported format version ({header?.FormatVersion ?? 0}).");
            if (header.Manifest?.Frames == null || header.FrameCount <= 0 || header.FrameCount != header.Manifest.Frames.Length || header.FrameCount > RemoteFrameRecorderLimits.MaximumCapacity)
                throw new InvalidDataException("The frame recording manifest is invalid.");

            var responses = new RemoteFrameRecorderFrameResponse[header.FrameCount];
            for (int i = 0; i < responses.Length; i++)
            {
                RemoteRecordedFrameInfo info = header.Manifest.Frames[i];
                if (info == null)
                    throw new InvalidDataException($"Frame recording manifest entry {i + 1} is invalid.");
                responses[i] = ReadJson<RemoteFrameRecorderFrameResponse>(archive, GetFrameEntryName(i));
                ValidateResponse(responses[i], header.Manifest.RecordingId, info.UnityFrame, i);
            }

            return new RemoteFrameRecordingArchive
            {
                Manifest = header.Manifest,
                FrameResponses = responses
            };
        }

        private static void ValidateResponse(RemoteFrameRecorderFrameResponse response, long recordingId, int unityFrame, int index)
        {
            if (response == null || response.RecordingId != recordingId || response.UnityFrame != unityFrame || !string.IsNullOrEmpty(response.Error))
                throw new InvalidDataException($"Stored frame {index + 1} does not match the recording manifest.");
            if (string.IsNullOrEmpty(response.ImageBase64))
                throw new InvalidDataException($"Stored frame {index + 1} has no image data.");
        }

        private static RemoteFrameRecorderFrameResponse CreateLegacyResponse(long recordingId, RemoteFrameReplayFrame frame)
        {
            string json = JsonUtility.ToJson(frame.SceneGraph ?? new RemoteRecordedSceneGraph());
            byte[] input = Encoding.UTF8.GetBytes(json);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Fastest, true))
                gzip.Write(input, 0, input.Length);
            return new RemoteFrameRecorderFrameResponse
            {
                RecordingId = recordingId,
                UnityFrame = frame.Info.UnityFrame,
                ImageBase64 = frame.ImageBase64,
                SceneGraphFormatVersion = RemoteRecordedSceneGraphFormats.LegacyFullSnapshot,
                SceneGraphGzipBase64 = Convert.ToBase64String(output.ToArray())
            };
        }

        private static void WriteJson<T>(ZipArchive archive, string entryName, T value)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(JsonUtility.ToJson(value));
        }

        private static T ReadJson<T>(ZipArchive archive, string entryName) where T : class
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName);
            if (entry == null)
                throw new InvalidDataException($"The frame recording is missing '{entryName}'.");
            if (entry.Length <= 0 || entry.Length > MaximumJsonEntryBytes)
                throw new InvalidDataException($"The frame recording entry '{entryName}' has an invalid size.");
            using Stream stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return JsonUtility.FromJson<T>(reader.ReadToEnd());
        }

        private static string GetFrameEntryName(int index) => "frames/" + index.ToString("D4", CultureInfo.InvariantCulture) + ".json";

        private static void ReplaceDestination(string temporaryPath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            string backupPath = destinationPath + ".backup-" + Guid.NewGuid().ToString("N");
            File.Move(destinationPath, backupPath);
            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(backupPath))
                    File.Move(backupPath, destinationPath);
                throw;
            }

            try
            {
                File.Delete(backupPath);
            }
            catch
            {
                // The new recording is already safely in place. A stale backup is preferable to
                // reporting a failed save or risking the destination while cleaning it up.
            }
        }
    }

    internal sealed class RemoteFrameReplayFrame
    {
        internal RemoteRecordedFrameInfo Info;
        internal string ImageBase64;
        internal RemoteRecordedSceneGraph SceneGraph;
        internal RemoteFrameRecorderFrameResponse SourceResponse;
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
        private readonly Dictionary<string, string> _knownInspectorBlobs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _decodedInspectorBlobs = new(StringComparer.Ordinal);
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
        internal bool CanSaveRecording => !IsDownloading && _replayFrames.Count > 0 && Manifest?.Frames?.Length == _replayFrames.Count;
        internal string RecordingFilePath { get; private set; }

        public void OnConnected() => Query();

        internal void Query()
        {
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest, new RemoteFrameRecorderControlRequest { Action = RemoteFrameRecorderAction.Query });
        }

        internal void Start(int capacity, int maximumWidth, int jpegQuality, RemoteFrameRecorderInspectorScope inspectorScope, long inspectedObjectId)
        {
            ClearReplay();
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest, new RemoteFrameRecorderControlRequest
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
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest, new RemoteFrameRecorderControlRequest
            {
                Action = RemoteFrameRecorderAction.Seal,
                FreezePlayerWhenSealed = freezePlayer
            });
        }

        internal void Release()
        {
            _controlRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ControlRequest, new RemoteFrameRecorderControlRequest { Action = RemoteFrameRecorderAction.Release });
        }

        internal bool SaveRecording(string path)
        {
            try
            {
                RemoteFrameRecordingStore.Save(path, Manifest, _replayFrames);
                RecordingFilePath = Path.GetFullPath(path);
                DownloadError = null;
                _session.NotifyStateChanged();
                return true;
            }
            catch (Exception exception)
            {
                DownloadError = "Could not save the frame recording. " + exception.Message;
                _session.NotifyStateChanged();
                return false;
            }
        }

        internal bool LoadRecording(string path)
        {
            try
            {
                RemoteFrameRecordingArchive archive = RemoteFrameRecordingStore.Load(path);
                var decoder = new RemoteFrameRecorderClient(_session);
                decoder.ImportRecording(archive);

                ClearReplay();
                Manifest = archive.Manifest;
                _replayFrames.AddRange(decoder._replayFrames);
                RecordingFilePath = Path.GetFullPath(path);
                DownloadError = null;
                _session.NotifyStateChanged();
                return true;
            }
            catch (Exception exception)
            {
                DownloadError = "Could not open the frame recording. " + exception.Message;
                _session.NotifyStateChanged();
                return false;
            }
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
            if (!string.IsNullOrEmpty(RecordingFilePath) && _replayFrames.Count > 0)
            {
                DownloadError = null;
                ResetDecodeState();
            }
            else
            {
                ClearReplay();
            }
        }

        private void HandleControl(RemoteEnvelope envelope)
        {
            if (envelope.RequestId != _controlRequestId)
                return;
            _controlRequestId = 0;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteFrameRecorderControlResponse response, out string error))
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

            if (response.Action == RemoteFrameRecorderAction.Seal && response.State == RemoteFrameRecorderState.Sealed && response.RecordingId != 0)
                RequestManifest(response.RecordingId);
        }

        private void RequestManifest(long recordingId)
        {
            _manifestRequestId = _session.Send(RemoteFrameRecorderMessageTypes.ManifestRequest, new RemoteFrameRecorderManifestRequest { RecordingId = recordingId });
        }

        private void HandleManifest(RemoteEnvelope envelope)
        {
            if (envelope.RequestId != _manifestRequestId)
                return;
            _manifestRequestId = 0;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteFrameRecorderManifestResponse response, out string error))
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

            _frameRequestId = _session.Send(RemoteFrameRecorderMessageTypes.FrameRequest, new RemoteFrameRecorderFrameRequest
            {
                RecordingId = Manifest.RecordingId,
                UnityFrame = frames[_nextDownloadIndex].UnityFrame,
                SupportedSceneGraphFormatVersion = RemoteRecordedSceneGraphFormats.ContentAddressedObjects,
                KnownHierarchySnapshotId = _knownHierarchySnapshotId,
                KnownInspectorSnapshotId = _knownInspectorSnapshotId
            });
        }

        private void HandleFrame(RemoteEnvelope envelope)
        {
            if (envelope.RequestId != _frameRequestId)
                return;
            _frameRequestId = 0;
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteFrameRecorderFrameResponse response, out string error))
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
                RemoteRecordedFrameInfo info = Manifest.Frames[_nextDownloadIndex];
                AddDecodedFrame(response, info);
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
            ResetDecodeState();
            RecordingFilePath = null;
        }

        private void ResetDecodeState()
        {
            _knownHierarchySnapshotId = null;
            _knownInspectorSnapshotId = null;
            _knownHierarchy = null;
            _knownInspector = null;
            _knownInspectorManifest = null;
            _knownInspectorBlobs.Clear();
            _decodedInspectorBlobs.Clear();
            _knownInspectorObjects.Clear();
        }

        private void ImportRecording(RemoteFrameRecordingArchive archive)
        {
            ClearReplay();
            Manifest = archive?.Manifest ?? throw new InvalidDataException("The frame recording has no manifest.");
            RemoteFrameRecorderFrameResponse[] responses = archive.FrameResponses ?? Array.Empty<RemoteFrameRecorderFrameResponse>();
            if (Manifest.Frames == null || Manifest.Frames.Length != responses.Length)
                throw new InvalidDataException("The frame recording is incomplete.");
            for (int i = 0; i < responses.Length; i++)
                AddDecodedFrame(responses[i], Manifest.Frames[i]);
        }

        private void AddDecodedFrame(RemoteFrameRecorderFrameResponse response, RemoteRecordedFrameInfo info)
        {
            if (response == null || info == null || response.RecordingId != Manifest.RecordingId || response.UnityFrame != info.UnityFrame)
                throw new InvalidDataException("The recorded frame does not match its manifest entry.");

            RemoteRecordedSceneGraph graph;
            if (response.SceneGraphFormatVersion >= RemoteRecordedSceneGraphFormats.ContentAddressedObjects)
                graph = ResolveObjectSectionedSceneGraph(response);
            else if (response.SceneGraphFormatVersion >= RemoteRecordedSceneGraphFormats.ContentAddressedSections)
                graph = ResolveSectionedSceneGraph(response);
            else
                graph = ResolveLegacySceneGraph(response);
            _replayFrames.Add(new RemoteFrameReplayFrame
            {
                Info = info,
                ImageBase64 = response.ImageBase64,
                SceneGraph = graph ?? new RemoteRecordedSceneGraph(),
                SourceResponse = response
            });
        }

        private RemoteRecordedSceneGraph ResolveSectionedSceneGraph(RemoteFrameRecorderFrameResponse response)
        {
            if (string.IsNullOrEmpty(response.HierarchySnapshotId) || string.IsNullOrEmpty(response.InspectorSnapshotId))
                throw new InvalidDataException("The recorded frame has incomplete scene-graph references.");

            if (!string.IsNullOrEmpty(response.HierarchyGzipBase64))
            {
                _knownHierarchy = DeserializeCompressed<RemoteSceneInspectorHierarchyResponse>(response.HierarchyGzipBase64);
                _knownHierarchySnapshotId = response.HierarchySnapshotId;
            }
            else if (!string.Equals(_knownHierarchySnapshotId, response.HierarchySnapshotId, StringComparison.Ordinal) || _knownHierarchy == null)
            {
                throw new InvalidDataException("The hierarchy delta references an unavailable snapshot.");
            }

            if (!string.IsNullOrEmpty(response.InspectorGzipBase64))
            {
                _knownInspector = DeserializeCompressed<RemoteRecordedInspectorSnapshot>(response.InspectorGzipBase64);
                _knownInspectorSnapshotId = response.InspectorSnapshotId;
            }
            else if (!string.Equals(_knownInspectorSnapshotId, response.InspectorSnapshotId, StringComparison.Ordinal) || _knownInspector == null)
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

        private RemoteRecordedSceneGraph ResolveObjectSectionedSceneGraph(RemoteFrameRecorderFrameResponse response)
        {
            ResolveHierarchy(response);
            if (string.IsNullOrEmpty(response.InspectorSnapshotId))
                throw new InvalidDataException("The recorded frame has no inspector manifest reference.");

            if (!string.IsNullOrEmpty(response.InspectorManifestGzipBase64))
            {
                _knownInspectorManifest = DeserializeCompressed<RemoteRecordedInspectorManifest>(response.InspectorManifestGzipBase64);
                _knownInspectorSnapshotId = response.InspectorSnapshotId;
            }
            else if (!string.Equals(_knownInspectorSnapshotId, response.InspectorSnapshotId, StringComparison.Ordinal) || _knownInspectorManifest == null)
            {
                throw new InvalidDataException("The inspector manifest references an unavailable snapshot.");
            }

            RemoteRecordedSceneGraphBlob[] suppliedBlobs = response.InspectorBlobs ?? Array.Empty<RemoteRecordedSceneGraphBlob>();
            for (int i = 0; i < suppliedBlobs.Length; i++)
            {
                RemoteRecordedSceneGraphBlob blob = suppliedBlobs[i];
                if (blob == null || string.IsNullOrEmpty(blob.SnapshotId) || string.IsNullOrEmpty(blob.GzipBase64))
                    throw new InvalidDataException("The inspector response contains an invalid object snapshot.");
                _knownInspectorBlobs[blob.SnapshotId] = blob.GzipBase64;
                _decodedInspectorBlobs.Remove(blob.SnapshotId);
            }

            RemoteRecordedObjectSnapshotReference[] references = _knownInspectorManifest.Objects ?? Array.Empty<RemoteRecordedObjectSnapshotReference>();
            var inspections = new RemoteObjectDetails[references.Length];
            var nextObjects = new Dictionary<long, CachedRemoteObject>();
            var requiredPayloads = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < references.Length; i++)
            {
                RemoteRecordedObjectSnapshotReference reference = references[i];
                if (reference == null || reference.IsNull)
                    continue;
                AddRequiredPayloads(reference, requiredPayloads);
                if (_knownInspectorObjects.TryGetValue(reference.ObjectId, out CachedRemoteObject cached) && cached.Matches(reference))
                {
                    inspections[i] = cached.Details;
                    nextObjects[reference.ObjectId] = cached;
                    continue;
                }

                RemoteRecordedObjectHeader header = ReadInspectorBlob<RemoteRecordedObjectHeader>(reference.HeaderSnapshotId);
                if (header == null || header.Id != reference.ObjectId)
                    throw new InvalidDataException("A recorded object header is invalid.");
                RemoteRecordedMaterialSnapshot material = ReadInspectorBlob<RemoteRecordedMaterialSnapshot>(reference.MaterialSnapshotId);
                string[] componentIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
                var components = new RemoteComponentDescriptor[componentIds.Length];
                for (int componentIndex = 0; componentIndex < componentIds.Length; componentIndex++)
                {
                    components[componentIndex] = ReadInspectorBlob<RemoteComponentDescriptor>(componentIds[componentIndex]);
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
                _knownHierarchy = DeserializeCompressed<RemoteSceneInspectorHierarchyResponse>(response.HierarchyGzipBase64);
                _knownHierarchySnapshotId = response.HierarchySnapshotId;
            }
            else if (!string.Equals(_knownHierarchySnapshotId, response.HierarchySnapshotId, StringComparison.Ordinal) || _knownHierarchy == null)
            {
                throw new InvalidDataException("The hierarchy delta references an unavailable snapshot.");
            }
        }

        private T ReadInspectorBlob<T>(string snapshotId) where T : class
        {
            if (string.IsNullOrEmpty(snapshotId) || !_knownInspectorBlobs.TryGetValue(snapshotId, out string base64))
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

        private static void AddRequiredPayloads(RemoteRecordedObjectSnapshotReference reference, ISet<string> destination)
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

        private static RemoteRecordedSceneGraph ResolveLegacySceneGraph(RemoteFrameRecorderFrameResponse response)
        {
            return DeserializeCompressed<RemoteRecordedSceneGraph>(response.SceneGraphGzipBase64) ?? new RemoteRecordedSceneGraph();
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

            internal CachedRemoteObject(RemoteRecordedObjectSnapshotReference reference, RemoteObjectDetails details)
            {
                _headerSnapshotId = reference.HeaderSnapshotId;
                _materialSnapshotId = reference.MaterialSnapshotId;
                _componentSnapshotIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
                Details = details;
            }

            internal RemoteObjectDetails Details { get; }

            internal bool Matches(RemoteRecordedObjectSnapshotReference reference)
            {
                if (!string.Equals(_headerSnapshotId, reference.HeaderSnapshotId, StringComparison.Ordinal) || !string.Equals(_materialSnapshotId, reference.MaterialSnapshotId, StringComparison.Ordinal))
                    return false;
                string[] componentIds = reference.ComponentSnapshotIds ?? Array.Empty<string>();
                if (_componentSnapshotIds.Length != componentIds.Length)
                    return false;
                for (int i = 0; i < componentIds.Length; i++)
                {
                    if (!string.Equals(_componentSnapshotIds[i], componentIds[i], StringComparison.Ordinal))
                        return false;
                }

                return true;
            }
        }
    }
}

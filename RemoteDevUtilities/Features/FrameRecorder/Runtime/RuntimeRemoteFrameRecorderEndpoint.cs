using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.FrameRecorder;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace SAS.Utilities.RemoteDevUtilities.FrameRecorder
{
    [Preserve]
    [RuntimeRemoteEndpoint("frame-recorder", 410)]
    internal sealed class RuntimeRemoteFrameRecorderEndpoint : IRuntimeRemoteEndpoint, IRuntimeRemoteSessionListener
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteFrameRecorderMessageTypes.ControlRequest,
            RemoteFrameRecorderMessageTypes.ManifestRequest,
            RemoteFrameRecorderMessageTypes.FrameRequest
        };

        private RuntimeRemoteEndpointContext _context;
        private RuntimeRemoteFrameRecorder _recorder;
        private long _nextRecordingId;
        private long _pendingSealRequestId;

        public IEnumerable<string> MessageTypes => SupportedMessages;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureRuntimeAssemblyIsLoaded()
        {
        }

        public void Initialize(RuntimeRemoteEndpointContext context)
        {
            _context = context;
            if (RuntimeDevUtilitiesAgent.Instance != null)
                _recorder = RuntimeDevUtilitiesAgent.Instance.gameObject.AddComponent<RuntimeRemoteFrameRecorder>();
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteFrameRecorderMessageTypes.ControlRequest:
                    HandleControl(envelope);
                    break;
                case RemoteFrameRecorderMessageTypes.ManifestRequest:
                    HandleManifest(envelope);
                    break;
                case RemoteFrameRecorderMessageTypes.FrameRequest:
                    HandleFrame(envelope);
                    break;
            }
        }

        public void Tick()
        {
            if (_pendingSealRequestId == 0 || _recorder == null || !_recorder.IsSealed)
                return;
            long requestId = _pendingSealRequestId;
            _pendingSealRequestId = 0;
            SendControl(requestId, RemoteFrameRecorderAction.Seal);
        }

        public void OnRemoteSessionStateChanged(bool active)
        {
            if (!active && _recorder != null && _recorder.IsRecording)
                _recorder.Seal(_recorder.RecordingId, false);
            if (!active)
                _pendingSealRequestId = 0;
        }

        public void Dispose()
        {
            if (_recorder != null)
            {
                _recorder.Release();
                UnityEngine.Object.Destroy(_recorder);
            }
            _recorder = null;
            _context = null;
            _pendingSealRequestId = 0;
        }

        private void HandleControl(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteFrameRecorderControlRequest request, out string error))
            {
                SendControlError(envelope.RequestId, RemoteFrameRecorderAction.Query, error);
                return;
            }

            if (_recorder == null)
            {
                SendControlError(envelope.RequestId, request.Action,
                    "Frame recording is not available in this Player.");
                return;
            }

            switch (request.Action)
            {
                case RemoteFrameRecorderAction.Query:
                    SendControl(envelope.RequestId, request.Action);
                    break;
                case RemoteFrameRecorderAction.Start:
                    _pendingSealRequestId = 0;
                    _recorder.StartRecording(++_nextRecordingId, request.Capacity,
                        request.MaximumWidth, request.JpegQuality, request.InspectorScope,
                        request.InspectedObjectId);
                    SendControl(envelope.RequestId, request.Action);
                    break;
                case RemoteFrameRecorderAction.Seal:
                    if (_recorder.RecordingId == 0)
                    {
                        SendControlError(envelope.RequestId, request.Action,
                            "There is no active frame recording to fetch.");
                        return;
                    }
                    _recorder.Seal(_recorder.RecordingId, request.FreezePlayerWhenSealed);
                    if (_recorder.IsSealed)
                        SendControl(envelope.RequestId, request.Action);
                    else
                        _pendingSealRequestId = envelope.RequestId;
                    break;
                case RemoteFrameRecorderAction.Release:
                    _pendingSealRequestId = 0;
                    _recorder.Release();
                    SendControl(envelope.RequestId, request.Action);
                    break;
                default:
                    SendControlError(envelope.RequestId, request.Action,
                        "The requested frame-recorder action is not supported.");
                    break;
            }
        }

        private void HandleManifest(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteFrameRecorderManifestRequest request, out string error))
            {
                SendManifestError(envelope.RequestId, 0, error);
                return;
            }

            if (_recorder == null || request.RecordingId != _recorder.RecordingId)
            {
                SendManifestError(envelope.RequestId, request.RecordingId,
                    "The requested frame recording is no longer available.");
                return;
            }

            if (!_recorder.IsSealed)
            {
                SendManifestError(envelope.RequestId, request.RecordingId,
                    "The frame recording is still being finalized.");
                return;
            }

            _context.Sender.Send(RemoteFrameRecorderMessageTypes.ManifestResponse, envelope.RequestId,
                new RemoteFrameRecorderManifestResponse
                {
                    RecordingId = request.RecordingId,
                    State = RemoteFrameRecorderState.Sealed,
                    Frames = _recorder.GetManifest(request.RecordingId)
                });
        }

        private void HandleFrame(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteFrameRecorderFrameRequest request, out string error))
            {
                SendFrameError(envelope.RequestId, 0, 0, error);
                return;
            }

            if (_recorder == null || !_recorder.TryGetFrame(request.RecordingId, request.UnityFrame,
                    out RuntimeRecordedFrameData frame))
            {
                SendFrameError(envelope.RequestId, request.RecordingId, request.UnityFrame,
                    "The requested recorded frame is no longer available.");
                return;
            }

            string imageBase64 = Convert.ToBase64String(frame.JpegBytes ?? Array.Empty<byte>());
            if (request.SupportedSceneGraphFormatVersion <
                RemoteRecordedSceneGraphFormats.ContentAddressedSections)
            {
                if (!TryBuildLegacySceneGraph(frame, out string legacyGraphBase64, out string legacyError))
                {
                    SendFrameError(envelope.RequestId, request.RecordingId, request.UnityFrame,
                        legacyError);
                    return;
                }
                if (imageBase64.Length + legacyGraphBase64.Length >
                    RemoteProtocolConstants.MaximumMessageBytes - 8192)
                {
                    SendFrameError(envelope.RequestId, request.RecordingId, request.UnityFrame,
                        "The recorded frame exceeded the remote message limit. Use a smaller capture width.");
                    return;
                }
                _context.Sender.Send(RemoteFrameRecorderMessageTypes.FrameResponse, envelope.RequestId,
                    new RemoteFrameRecorderFrameResponse
                    {
                        RecordingId = request.RecordingId,
                        UnityFrame = request.UnityFrame,
                        ImageBase64 = imageBase64,
                        SceneGraphGzipBase64 = legacyGraphBase64
                    });
                return;
            }

            string hierarchyBase64 = string.Empty;
            if (!string.Equals(request.KnownHierarchySnapshotId, frame.HierarchySnapshotId,
                    StringComparison.Ordinal))
            {
                if (!_recorder.TryGetSceneGraphBlob(frame.HierarchySnapshotId, out byte[] hierarchyBytes))
                {
                    SendFrameError(envelope.RequestId, request.RecordingId, request.UnityFrame,
                        "The recorded hierarchy snapshot is no longer available.");
                    return;
                }
                hierarchyBase64 = Convert.ToBase64String(hierarchyBytes);
            }

            string inspectorBase64 = string.Empty;
            if (!string.Equals(request.KnownInspectorSnapshotId, frame.InspectorSnapshotId,
                    StringComparison.Ordinal))
            {
                if (!_recorder.TryGetSceneGraphBlob(frame.InspectorSnapshotId, out byte[] inspectorBytes))
                {
                    SendFrameError(envelope.RequestId, request.RecordingId, request.UnityFrame,
                        "The recorded inspector snapshot is no longer available.");
                    return;
                }
                inspectorBase64 = Convert.ToBase64String(inspectorBytes);
            }

            if (imageBase64.Length + hierarchyBase64.Length + inspectorBase64.Length >
                RemoteProtocolConstants.MaximumMessageBytes - 8192)
            {
                SendFrameError(envelope.RequestId, request.RecordingId, request.UnityFrame,
                    "The recorded frame exceeded the remote message limit. Use a smaller capture width.");
                return;
            }

            _context.Sender.Send(RemoteFrameRecorderMessageTypes.FrameResponse, envelope.RequestId,
                new RemoteFrameRecorderFrameResponse
                {
                    RecordingId = request.RecordingId,
                    UnityFrame = request.UnityFrame,
                    ImageBase64 = imageBase64,
                    SceneGraphFormatVersion = RemoteRecordedSceneGraphFormats.ContentAddressedSections,
                    HierarchySnapshotId = frame.HierarchySnapshotId,
                    HierarchyGzipBase64 = hierarchyBase64,
                    InspectorSnapshotId = frame.InspectorSnapshotId,
                    InspectorGzipBase64 = inspectorBase64
                });
        }

        private bool TryBuildLegacySceneGraph(RuntimeRecordedFrameData frame, out string base64,
            out string error)
        {
            base64 = null;
            error = null;
            try
            {
                if (!_recorder.TryGetSceneGraphBlob(frame.HierarchySnapshotId,
                        out byte[] hierarchyBytes) ||
                    !_recorder.TryGetSceneGraphBlob(frame.InspectorSnapshotId,
                        out byte[] inspectorBytes))
                {
                    error = "The recorded scene graph is no longer available.";
                    return false;
                }

                RemoteSceneInspectorHierarchyResponse hierarchy =
                    JsonUtility.FromJson<RemoteSceneInspectorHierarchyResponse>(
                        Encoding.UTF8.GetString(Decompress(hierarchyBytes)));
                RemoteRecordedInspectorSnapshot inspector =
                    JsonUtility.FromJson<RemoteRecordedInspectorSnapshot>(
                        Encoding.UTF8.GetString(Decompress(inspectorBytes)));
                var graph = new RemoteRecordedSceneGraph
                {
                    Hierarchy = hierarchy ?? new RemoteSceneInspectorHierarchyResponse(),
                    Inspections = inspector?.Inspections ?? Array.Empty<RemoteObjectDetails>(),
                    Error = inspector?.Error
                };
                byte[] json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(graph));
                base64 = Convert.ToBase64String(Compress(json));
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
        }

        private static byte[] Compress(byte[] bytes)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output,
                       System.IO.Compression.CompressionLevel.Fastest, true))
                gzip.Write(bytes, 0, bytes.Length);
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] bytes)
        {
            using var input = new MemoryStream(bytes ?? Array.Empty<byte>());
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private void SendControl(long requestId, RemoteFrameRecorderAction action)
        {
            RemoteRecordedFrameInfo[] manifest = _recorder?.GetManifest(_recorder.RecordingId) ??
                                                 Array.Empty<RemoteRecordedFrameInfo>();
            int firstFrame = manifest.Length > 0 ? manifest[0].UnityFrame : 0;
            int lastFrame = manifest.Length > 0 ? manifest[manifest.Length - 1].UnityFrame : 0;
            RemoteFrameRecorderState state = GetState();
            _context?.Sender.Send(RemoteFrameRecorderMessageTypes.ControlResponse, requestId,
                new RemoteFrameRecorderControlResponse
                {
                    Action = action,
                    State = state,
                    RecordingId = _recorder?.RecordingId ?? 0,
                    Capacity = _recorder?.Capacity ?? 0,
                    CapturedFrameCount = _recorder?.CapturedFrameCount ?? 0,
                    PendingFrameCount = _recorder?.PendingFrameCount ?? 0,
                    MissedFrameCount = _recorder?.MissedFrameCount ?? 0,
                    FirstUnityFrame = firstFrame,
                    LastUnityFrame = lastFrame,
                    StoredBytes = _recorder?.StoredBytes ?? 0,
                    SceneGraphBytesSaved = _recorder?.SceneGraphBytesSaved ?? 0,
                    UsesAsyncGpuReadback = _recorder?.UsesAsyncGpuReadback == true,
                    PlayerFrozen = _recorder?.PlayerFrozen == true,
                    InspectorScope = _recorder?.InspectorScope ??
                                     RemoteFrameRecorderInspectorScope.HierarchyOnly,
                    InspectedObjectId = _recorder?.InspectedObjectId ?? 0,
                    Warning = _recorder?.LastError ?? string.Empty
                });
        }

        private RemoteFrameRecorderState GetState()
        {
            if (_recorder == null || _recorder.RecordingId == 0)
                return RemoteFrameRecorderState.Idle;
            if (_recorder.IsRecording)
                return RemoteFrameRecorderState.Recording;
            if (_recorder.IsFinalizing)
                return RemoteFrameRecorderState.Finalizing;
            return RemoteFrameRecorderState.Sealed;
        }

        private void SendControlError(long requestId, RemoteFrameRecorderAction action, string error)
        {
            _context?.Sender.Send(RemoteFrameRecorderMessageTypes.ControlResponse, requestId,
                new RemoteFrameRecorderControlResponse
                {
                    Action = action,
                    State = GetState(),
                    RecordingId = _recorder?.RecordingId ?? 0,
                    Error = error ?? "The frame-recorder request failed."
                });
        }

        private void SendManifestError(long requestId, long recordingId, string error)
        {
            _context?.Sender.Send(RemoteFrameRecorderMessageTypes.ManifestResponse, requestId,
                new RemoteFrameRecorderManifestResponse
                {
                    RecordingId = recordingId,
                    State = GetState(),
                    Error = error ?? "The frame-recorder manifest request failed."
                });
        }

        private void SendFrameError(long requestId, long recordingId, int unityFrame, string error)
        {
            _context?.Sender.Send(RemoteFrameRecorderMessageTypes.FrameResponse, requestId,
                new RemoteFrameRecorderFrameResponse
                {
                    RecordingId = recordingId,
                    UnityFrame = unityFrame,
                    Error = error ?? "The recorded frame request failed."
                });
        }
    }
}

using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.Capture;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RuntimeSceneInspector;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine;
using RemoteMessageTypes = SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.RemoteSceneInspectorMessageTypes;

namespace SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector.Capture
{
    internal sealed class RemoteRuntimeSceneCaptureFeature : IDisposable
    {
        private RuntimeRemoteEndpointContext _context;
        private readonly RuntimeSceneInspectorService _service;
        private readonly RuntimeSceneObjectPicker _picker;
        private readonly bool _allowObjectPicking;
        private RuntimeRemoteSceneCaptureRunner _captureRunner;
        private long _nextCaptureId;

        internal RemoteRuntimeSceneCaptureFeature(RuntimeRemoteEndpointContext context,
            RuntimeSceneInspectorService service, RuntimeSceneInspectorSettings settings)
        {
            _context = context;
            _service = service;
            if (service == null || settings == null)
                return;

            _allowObjectPicking = settings.AllowObjectPicking;
            _picker = new RuntimeSceneObjectPicker(settings, service);
            if (RuntimeDevUtilitiesAgent.Instance != null)
                _captureRunner = RuntimeDevUtilitiesAgent.Instance.gameObject.AddComponent<RuntimeRemoteSceneCaptureRunner>();
        }

        internal void Capture(RemoteEnvelope envelope)
        {
            if (_service == null)
            {
                SendCaptureError(envelope.RequestId, "The remote Runtime Scene Inspector is disabled.");
                return;
            }

            if (!_allowObjectPicking)
            {
                SendCaptureError(envelope.RequestId,
                    "Object picking is disabled by the Runtime Scene Inspector settings.");
                return;
            }

            if (_captureRunner == null)
            {
                SendCaptureError(envelope.RequestId, "Player screen capture is not available.");
                return;
            }

            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneCaptureRequest request,
                out string error))
            {
                SendCaptureError(envelope.RequestId, error);
                return;
            }

            long captureId = ++_nextCaptureId;
            _captureRunner.Capture(captureId, request.MaximumWidth, request.JpegQuality,
                request.FreezeWhilePicking, result => SendCaptureResult(envelope.RequestId, result));
        }

        internal void Pick(RemoteEnvelope envelope)
        {
            if (_service == null)
            {
                SendPickResult(envelope.RequestId,
                    new RemoteScenePickResponse { Error = "The remote Runtime Scene Inspector is disabled." });
                return;
            }

            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteScenePickRequest request,
                out string error))
            {
                SendPickResult(envelope.RequestId, new RemoteScenePickResponse { Error = error });
                return;
            }

            if (request.Cancel)
            {
                long activeCaptureId = _captureRunner?.ActiveCaptureId ?? 0;
                if (_captureRunner == null ||
                    (request.CaptureId > 0 && request.CaptureId != activeCaptureId))
                {
                    SendPickResult(envelope.RequestId, new RemoteScenePickResponse
                    {
                        CaptureId = request.CaptureId,
                        Cancelled = true,
                        Error = "The captured frame is no longer active."
                    });
                    return;
                }

                _captureRunner.Release(request.CaptureId);
                SendPickResult(envelope.RequestId, new RemoteScenePickResponse
                {
                    CaptureId = request.CaptureId > 0 ? request.CaptureId : activeCaptureId,
                    Cancelled = true
                });
                return;
            }

            if (_captureRunner == null || request.CaptureId <= 0 ||
                request.CaptureId != _captureRunner.ActiveCaptureId)
            {
                SendPickResult(envelope.RequestId, new RemoteScenePickResponse
                {
                    CaptureId = request.CaptureId,
                    Cancelled = true,
                    Error = "The captured frame is no longer active. Capture the Player again."
                });
                return;
            }

            if (request.NormalizedX < 0f || request.NormalizedX > 1f ||
                request.NormalizedY < 0f || request.NormalizedY > 1f)
            {
                SendPickResult(envelope.RequestId, new RemoteScenePickResponse
                {
                    CaptureId = request.CaptureId,
                    Error = "The capture coordinate was outside the Player screen."
                });
                return;
            }

            _service.RefreshHierarchy();
            Vector2 screenPosition = new(request.NormalizedX * Screen.width, request.NormalizedY * Screen.height);
            IReadOnlyList<RuntimeScenePickCandidate> runtimeCandidates =
                _picker.GetCandidates(screenPosition, out string pickError);
            var remoteCandidates = new RemoteScenePickCandidate[runtimeCandidates.Count];
            for (int i = 0; i < runtimeCandidates.Count; i++)
            {
                RuntimeScenePickCandidate candidate = runtimeCandidates[i];
                remoteCandidates[i] = new RemoteScenePickCandidate
                {
                    ObjectId = candidate.ObjectId.Value,
                    Name = candidate.Name,
                    HierarchyPath = candidate.HierarchyPath,
                    Source = candidate.Source.ToString()
                };
            }

            bool found = remoteCandidates.Length > 0;
            SendPickResult(envelope.RequestId, new RemoteScenePickResponse
            {
                CaptureId = request.CaptureId,
                Found = found,
                ObjectId = found ? remoteCandidates[0].ObjectId : 0,
                Candidates = remoteCandidates,
                Error = found ? string.Empty : pickError
            });
        }

        internal void OnSessionStateChanged(bool active)
        {
            if (!active)
                _captureRunner?.Release();
        }

        public void Dispose()
        {
            if (_captureRunner != null)
            {
                _captureRunner.Release();
                UnityEngine.Object.Destroy(_captureRunner);
            }

            _captureRunner = null;
            _context = null;
        }

        private void SendCaptureResult(long requestId, RuntimeRemoteSceneCaptureResult result)
        {
            if (_context == null || result == null)
                return;

            string imageBase64 = result.JpegBytes == null ? string.Empty : Convert.ToBase64String(result.JpegBytes);
            if (!string.IsNullOrEmpty(result.Error) || string.IsNullOrEmpty(imageBase64))
                _captureRunner?.Release(result.CaptureId);
            if (imageBase64.Length > RemoteProtocolConstants.MaximumMessageBytes - 4096)
            {
                _captureRunner?.Release(result.CaptureId);
                SendCaptureError(requestId, "The encoded Player capture exceeded the remote message limit.");
                return;
            }

            _context.Sender.Send(RemoteSceneInspectorMessageTypes.SceneInspectorCaptureResponse, requestId,
                new RemoteSceneCaptureResponse
                {
                    CaptureId = result.CaptureId,
                    ImageBase64 = imageBase64,
                    Width = result.Width,
                    Height = result.Height,
                    FrameCount = result.FrameCount,
                    PlayerFrozen = result.PlayerFrozen,
                    Error = result.Error ?? string.Empty
                });
        }

        private void SendCaptureError(long requestId, string error)
        {
            _context?.Sender.Send(RemoteSceneInspectorMessageTypes.SceneInspectorCaptureResponse, requestId,
                new RemoteSceneCaptureResponse { Error = error ?? "Player capture failed." });
        }

        private void SendPickResult(long requestId, RemoteScenePickResponse response)
        {
            _context?.Sender.Send(RemoteSceneInspectorMessageTypes.SceneInspectorPickResponse, requestId,
                response ?? new RemoteScenePickResponse { Error = "Remote picking failed." });
        }
    }
}

using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.Capture;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using RemoteMessageTypes = SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.RemoteSceneInspectorMessageTypes;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    [RemoteEditorFeature("runtime-scene-inspector", 400)]
    internal sealed class RemoteRuntimeSceneInspectorClient : IRemoteEditorFeatureClient
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteSceneInspectorMessageTypes.SceneInspectorHierarchyResponse,
            RemoteSceneInspectorMessageTypes.SceneInspectorInspectResponse,
            RemoteSceneInspectorMessageTypes.SceneInspectorCommandResponse,
            RemoteSceneInspectorMessageTypes.SceneInspectorCaptureResponse,
            RemoteSceneInspectorMessageTypes.SceneInspectorPickResponse
        };

        private readonly IRemoteEditorSession _session;
        private long _inspectionRequestId;
        private long _captureRequestId;
        private long _pickRequestId;

        public RemoteRuntimeSceneInspectorClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public RemoteSceneInspectorHierarchyResponse Hierarchy { get; private set; } = new();
        public RemoteSceneInspectorInspectResponse Inspection { get; private set; }
        public long InspectionRevision { get; private set; }
        public long InspectionObjectId { get; private set; }
        public RemoteSceneInspectorCommandResponse LastCommandResult { get; private set; }
        public RemoteSceneCaptureResponse Capture { get; private set; }
        public RemoteScenePickResponse LastPickResult { get; private set; }
        public long LastPickedObjectId { get; private set; }
        public int PickRevision { get; private set; }
        public bool IsCapturePending => _captureRequestId != 0 && Capture == null;
        public bool IsPickPending => _pickRequestId != 0 && LastPickResult == null;
        public bool IsCaptureActive => Capture != null && Capture.CaptureId > 0 && string.IsNullOrEmpty(Capture.Error) &&
                                       !(LastPickResult?.CaptureId == Capture.CaptureId &&
                                         LastPickResult.Cancelled);

        public void RequestHierarchy(bool forceRefresh)
        {
            _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorHierarchyRequest, new RemoteSceneInspectorHierarchyRequest { ForceRefresh = forceRefresh });
        }

        public void Refresh(long selectedObjectId)
        {
            RequestHierarchy(true);
            if (selectedObjectId > 0)
                Inspect(selectedObjectId);
        }

        public void OnConnected() => RequestHierarchy(true);

        public void Inspect(long objectId)
        {
            Inspection = null;
            InspectionObjectId = objectId;
            _inspectionRequestId = _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorInspectRequest, new RemoteSceneInspectorInspectRequest { ObjectId = objectId });
        }

        public void Execute(RemoteSceneInspectorCommandRequest command)
        {
            LastCommandResult = null;
            _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorCommandRequest, command);
        }

        public void RequestCapture(bool freezeWhilePicking, int maximumWidth = 960, int jpegQuality = 70)
        {
            Capture = null;
            LastPickResult = null;
            _captureRequestId = _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorCaptureRequest,
                new RemoteSceneCaptureRequest
                {
                    FreezeWhilePicking = freezeWhilePicking,
                    MaximumWidth = maximumWidth,
                    JpegQuality = jpegQuality
                });
        }

        public void Pick(long captureId, float normalizedX, float normalizedY)
        {
            LastPickResult = null;
            _pickRequestId = _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorPickRequest,
                new RemoteScenePickRequest
                {
                    CaptureId = captureId,
                    NormalizedX = normalizedX,
                    NormalizedY = normalizedY
                });
        }

        public void ReleaseCapture()
        {
            if (!IsCaptureActive)
                return;

            LastPickResult = null;
            _pickRequestId = _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorPickRequest,
                new RemoteScenePickRequest { CaptureId = Capture.CaptureId, Cancel = true });
        }

        public void SelectPickedObject(long objectId)
        {
            if (objectId <= 0)
                return;
            LastPickedObjectId = objectId;
            PickRevision++;
            RequestHierarchy(true);
            Inspect(objectId);
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteSceneInspectorMessageTypes.SceneInspectorHierarchyResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorHierarchyResponse hierarchy, out _))
                        Hierarchy = hierarchy;
                    break;
                case RemoteSceneInspectorMessageTypes.SceneInspectorInspectResponse:
                    if (envelope.RequestId != _inspectionRequestId)
                        break;
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorInspectResponse inspection, out _))
                    {
                        Inspection = inspection;
                        InspectionRevision++;
                    }
                    break;
                case RemoteSceneInspectorMessageTypes.SceneInspectorCommandResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorCommandResponse commandResult, out _))
                    {
                        LastCommandResult = commandResult;
                        if (commandResult.Success)
                        {
                            RequestHierarchy(false);
                            if (Inspection?.Details != null)
                                Inspect(Inspection.Details.Id);
                        }
                    }

                    break;
                case RemoteSceneInspectorMessageTypes.SceneInspectorCaptureResponse:
                    if (envelope.RequestId != _captureRequestId)
                        break;
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneCaptureResponse capture, out _))
                        Capture = capture;
                    _captureRequestId = 0;
                    break;
                case RemoteSceneInspectorMessageTypes.SceneInspectorPickResponse:
                    if (envelope.RequestId != _pickRequestId)
                        break;
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteScenePickResponse pick, out _))
                    {
                        LastPickResult = pick;
                        if (Capture != null && Capture.CaptureId == pick.CaptureId && pick.Cancelled)
                            Capture.PlayerFrozen = false;
                        if (pick.Found && pick.ObjectId > 0)
                            SelectPickedObject(pick.ObjectId);
                    }
                    _pickRequestId = 0;
                    break;
            }

            _session.NotifyStateChanged();
        }

        public void Reset()
        {
            Hierarchy = new RemoteSceneInspectorHierarchyResponse();
            Inspection = null;
            InspectionRevision = 0;
            InspectionObjectId = 0;
            _inspectionRequestId = 0;
            _captureRequestId = 0;
            _pickRequestId = 0;
            LastCommandResult = null;
            Capture = null;
            LastPickResult = null;
            LastPickedObjectId = 0;
            PickRevision = 0;
        }
    }
}

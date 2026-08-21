using System;
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
        private bool _captureReleasePending;
        private readonly Dictionary<long, RemoteSceneInspectorCommandKind> _pendingCommands = new();
        private readonly Dictionary<long, RemoteObjectDetails> _recordedInspections = new();
        private bool _recordedReplay;

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
        public int SessionGeneration { get; private set; }
        public bool IsCapturePending => _captureRequestId != 0 && Capture == null;
        public bool IsCaptureReleasePending => _captureReleasePending;
        public bool IsPickPending => !_captureReleasePending && _pickRequestId != 0 && LastPickResult == null;
        public bool CanReleaseCapture => !_captureReleasePending &&
                                         (IsCapturePending || IsCaptureActive || IsPickPending);
        public bool IsCaptureActive => Capture != null && Capture.CaptureId > 0 && string.IsNullOrEmpty(Capture.Error) &&
                                       !(LastPickResult?.CaptureId == Capture.CaptureId &&
                                         LastPickResult.Cancelled);
        internal bool IsRecordedReplay => _recordedReplay;

        public void RequestHierarchy(bool forceRefresh)
        {
            if (_recordedReplay)
                return;
            _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorHierarchyRequest, new RemoteSceneInspectorHierarchyRequest { ForceRefresh = forceRefresh });
        }

        public void Refresh(long selectedObjectId)
        {
            RequestHierarchy(true);
            if (selectedObjectId > 0)
                RequestInspection(selectedObjectId, selectedObjectId != InspectionObjectId);
        }

        public void OnConnected() => RequestHierarchy(true);

        public void Inspect(long objectId) => RequestInspection(objectId, true);

        private void RequestInspection(long objectId, bool clearCurrent)
        {
            if (_recordedReplay)
            {
                InspectionObjectId = objectId;
                Inspection = _recordedInspections.TryGetValue(objectId, out RemoteObjectDetails details)
                    ? new RemoteSceneInspectorInspectResponse { Found = true, Details = details }
                    : new RemoteSceneInspectorInspectResponse
                    {
                        Error = "This object was not available in the recorded frame."
                    };
                InspectionRevision++;
                _session.NotifyStateChanged();
                return;
            }

            if (clearCurrent || InspectionObjectId != objectId)
                Inspection = null;
            InspectionObjectId = objectId;
            _inspectionRequestId = _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorInspectRequest, new RemoteSceneInspectorInspectRequest { ObjectId = objectId });
        }

        public void Execute(RemoteSceneInspectorCommandRequest command)
        {
            if (_recordedReplay)
            {
                LastCommandResult = new RemoteSceneInspectorCommandResponse
                {
                    Message = "Recorded frame inspection is read-only."
                };
                _session.NotifyStateChanged();
                return;
            }

            LastCommandResult = null;
            long requestId = _session.Send(
                RemoteSceneInspectorMessageTypes.SceneInspectorCommandRequest, command);
            if (requestId > 0 && command != null)
                _pendingCommands[requestId] = command.Kind;
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
            if (!CanReleaseCapture)
                return;

            long captureId = Capture?.CaptureId ?? 0;
            _captureRequestId = 0;
            Capture = null;
            LastPickResult = null;
            _pickRequestId = _session.Send(RemoteSceneInspectorMessageTypes.SceneInspectorPickRequest,
                new RemoteScenePickRequest { CaptureId = captureId, Cancel = true });
            _captureReleasePending = _pickRequestId != 0;
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
                    if (!_pendingCommands.Remove(envelope.RequestId,
                            out RemoteSceneInspectorCommandKind commandKind))
                        break;
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorCommandResponse commandResult, out _))
                    {
                        LastCommandResult = commandResult;
                        if (commandResult.Success)
                        {
                            if (CommandAffectsHierarchy(commandKind))
                                RequestHierarchy(false);
                            if (Inspection?.Details != null)
                                RequestInspection(Inspection.Details.Id, false);
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
                    _captureReleasePending = false;
                    break;
            }

            _session.NotifyStateChanged();
        }

        public void Reset()
        {
            SessionGeneration++;
            Hierarchy = new RemoteSceneInspectorHierarchyResponse();
            Inspection = null;
            InspectionRevision = 0;
            InspectionObjectId = 0;
            _inspectionRequestId = 0;
            _captureRequestId = 0;
            _pickRequestId = 0;
            _captureReleasePending = false;
            LastCommandResult = null;
            Capture = null;
            LastPickResult = null;
            LastPickedObjectId = 0;
            PickRevision = 0;
            _pendingCommands.Clear();
            _recordedInspections.Clear();
            _recordedReplay = false;
        }

        internal void LoadRecordedSnapshot(RemoteSceneInspectorHierarchyResponse hierarchy,
            RemoteObjectDetails[] inspections, int replayGeneration)
        {
            bool newReplay = !_recordedReplay || SessionGeneration != replayGeneration;
            _recordedReplay = true;
            SessionGeneration = replayGeneration;
            Hierarchy = hierarchy ?? new RemoteSceneInspectorHierarchyResponse();
            _recordedInspections.Clear();
            foreach (RemoteObjectDetails details in inspections ?? Array.Empty<RemoteObjectDetails>())
            {
                if (details == null)
                    continue;
                MakeReadOnly(details);
                _recordedInspections[details.Id] = details;
            }

            LastCommandResult = null;
            if (!newReplay && InspectionObjectId > 0 &&
                _recordedInspections.TryGetValue(InspectionObjectId, out RemoteObjectDetails selected))
            {
                Inspection = new RemoteSceneInspectorInspectResponse { Found = true, Details = selected };
            }
            else
            {
                Inspection = null;
                InspectionObjectId = 0;
            }
            InspectionRevision++;
        }

        private static void MakeReadOnly(RemoteObjectDetails details)
        {
            details.ActiveReadOnly = true;
            details.LayerReadOnly = true;
            foreach (RemoteComponentDescriptor component in
                     details.Components ?? Array.Empty<RemoteComponentDescriptor>())
            {
                if (component == null)
                    continue;
                component.EnabledReadOnly = true;
                foreach (RemoteMemberDescriptor member in
                         component.Members ?? Array.Empty<RemoteMemberDescriptor>())
                {
                    if (member != null)
                        member.ReadOnly = true;
                }
            }

            foreach (RemoteRendererMaterialDescriptor renderer in
                     details.MaterialsAndShaders?.Renderers ?? Array.Empty<RemoteRendererMaterialDescriptor>())
            foreach (RemoteMaterialSlotDescriptor slot in
                     renderer?.MaterialSlots ?? Array.Empty<RemoteMaterialSlotDescriptor>())
            {
                foreach (RemoteMaterialScopeState scope in
                         slot?.Scopes ?? Array.Empty<RemoteMaterialScopeState>())
                {
                    if (scope != null)
                        scope.ReadOnly = true;
                }
                foreach (RemoteShaderPropertyView property in
                         slot?.Properties ?? Array.Empty<RemoteShaderPropertyView>())
                {
                    if (property == null)
                        continue;
                    property.ReadOnly = true;
                    foreach (RemoteShaderPropertyScopeView scope in
                             property.Scopes ?? Array.Empty<RemoteShaderPropertyScopeView>())
                    {
                        if (scope != null)
                            scope.ReadOnly = true;
                    }
                }
            }
        }

        private static bool CommandAffectsHierarchy(RemoteSceneInspectorCommandKind kind) =>
            kind == RemoteSceneInspectorCommandKind.SetGameObjectActive;
    }
}

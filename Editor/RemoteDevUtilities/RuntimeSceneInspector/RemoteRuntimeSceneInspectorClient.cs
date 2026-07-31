using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector
{
    internal sealed class RemoteRuntimeSceneInspectorClient : IRemoteEditorFeatureClient
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.SceneInspectorHierarchyResponse,
            RemoteMessageTypes.SceneInspectorInspectResponse,
            RemoteMessageTypes.SceneInspectorCommandResponse
        };

        private readonly IRemoteEditorSession _session;
        private long _inspectionRequestId;

        public RemoteRuntimeSceneInspectorClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public RemoteSceneInspectorHierarchyResponse Hierarchy { get; private set; } = new();
        public RemoteSceneInspectorInspectResponse Inspection { get; private set; }
        public long InspectionObjectId { get; private set; }
        public RemoteSceneInspectorCommandResponse LastCommandResult { get; private set; }

        public void RequestHierarchy(bool forceRefresh)
        {
            _session.Send(RemoteMessageTypes.SceneInspectorHierarchyRequest, new RemoteSceneInspectorHierarchyRequest { ForceRefresh = forceRefresh });
        }

        public void Inspect(long objectId)
        {
            Inspection = null;
            InspectionObjectId = objectId;
            _inspectionRequestId = _session.Send(RemoteMessageTypes.SceneInspectorInspectRequest, new RemoteSceneInspectorInspectRequest { ObjectId = objectId });
        }

        public void Execute(RemoteSceneInspectorCommandRequest command)
        {
            LastCommandResult = null;
            _session.Send(RemoteMessageTypes.SceneInspectorCommandRequest, command);
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.SceneInspectorHierarchyResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorHierarchyResponse hierarchy, out _))
                        Hierarchy = hierarchy;
                    break;
                case RemoteMessageTypes.SceneInspectorInspectResponse:
                    if (envelope.RequestId != _inspectionRequestId)
                        break;
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorInspectResponse inspection, out _))
                        Inspection = inspection;
                    break;
                case RemoteMessageTypes.SceneInspectorCommandResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteSceneInspectorCommandResponse commandResult, out _))
                    {
                        LastCommandResult = commandResult;
                        if (commandResult.Success && Inspection?.Details != null)
                        {
                            RequestHierarchy(false);
                            Inspect(Inspection.Details.Id);
                        }
                    }

                    break;
            }

            _session.NotifyStateChanged();
        }

        public void Reset()
        {
            Hierarchy = new RemoteSceneInspectorHierarchyResponse();
            Inspection = null;
            InspectionObjectId = 0;
            _inspectionRequestId = 0;
            LastCommandResult = null;
        }
    }
}

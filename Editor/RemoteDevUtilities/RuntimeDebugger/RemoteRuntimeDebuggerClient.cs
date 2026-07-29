using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeDebugger;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.RuntimeDebugger
{
    internal sealed class RemoteRuntimeDebuggerClient : IRemoteEditorFeatureClient
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.DebuggerHierarchyResponse,
            RemoteMessageTypes.DebuggerInspectResponse,
            RemoteMessageTypes.DebuggerCommandResponse
        };

        private readonly IRemoteEditorSession _session;
        private long _inspectionRequestId;

        public RemoteRuntimeDebuggerClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public RemoteDebuggerHierarchyResponse Hierarchy { get; private set; } =
            new();
        public RemoteDebuggerInspectResponse Inspection { get; private set; }
        public long InspectionObjectId { get; private set; }
        public RemoteDebuggerCommandResponse LastCommandResult { get; private set; }

        public void RequestHierarchy(bool forceRefresh)
        {
            _session.Send(
                RemoteMessageTypes.DebuggerHierarchyRequest,
                new RemoteDebuggerHierarchyRequest { ForceRefresh = forceRefresh });
        }

        public void Inspect(long objectId)
        {
            Inspection = null;
            InspectionObjectId = objectId;
            _inspectionRequestId = _session.Send(
                RemoteMessageTypes.DebuggerInspectRequest,
                new RemoteDebuggerInspectRequest { ObjectId = objectId });
        }

        public void Execute(RemoteDebuggerCommandRequest command)
        {
            LastCommandResult = null;
            _session.Send(RemoteMessageTypes.DebuggerCommandRequest, command);
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.DebuggerHierarchyResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(
                            envelope,
                            out RemoteDebuggerHierarchyResponse hierarchy,
                            out _))
                        Hierarchy = hierarchy;
                    break;
                case RemoteMessageTypes.DebuggerInspectResponse:
                    if (envelope.RequestId != _inspectionRequestId)
                        break;
                    if (RemoteProtocolSerializer.TryDeserializePayload(
                            envelope,
                            out RemoteDebuggerInspectResponse inspection,
                            out _))
                        Inspection = inspection;
                    break;
                case RemoteMessageTypes.DebuggerCommandResponse:
                    if (RemoteProtocolSerializer.TryDeserializePayload(
                            envelope,
                            out RemoteDebuggerCommandResponse commandResult,
                            out _))
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
            Hierarchy = new RemoteDebuggerHierarchyResponse();
            Inspection = null;
            InspectionObjectId = 0;
            _inspectionRequestId = 0;
            LastCommandResult = null;
        }
    }
}

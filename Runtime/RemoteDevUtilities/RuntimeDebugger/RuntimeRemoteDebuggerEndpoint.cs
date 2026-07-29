using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeDebugger;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RuntimeDebugger;
using SAS.Utilities.RuntimeDebugger.Core;

namespace SAS.Utilities.RemoteDevUtilities.RuntimeDebugger
{
    internal sealed class RuntimeRemoteDebuggerEndpoint : IRuntimeRemoteEndpoint
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.DebuggerHierarchyRequest,
            RemoteMessageTypes.DebuggerInspectRequest,
            RemoteMessageTypes.DebuggerCommandRequest
        };

        private RuntimeRemoteEndpointContext _context;
        private RuntimeDebuggerService _service;

        public IEnumerable<string> MessageTypes => SupportedMessages;

        public void Initialize(RuntimeRemoteEndpointContext context)
        {
            _context = context;
            if (context.Settings.AllowRuntimeDebugger)
                _service = new RuntimeDebuggerService(RuntimeDebuggerSettings.LoadOrCreateDefaults());
        }

        public void Handle(RemoteEnvelope envelope)
        {
            if (_service == null)
            {
                SendUnavailable(envelope);
                return;
            }

            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.DebuggerHierarchyRequest:
                    SendHierarchy(envelope);
                    break;
                case RemoteMessageTypes.DebuggerInspectRequest:
                    Inspect(envelope);
                    break;
                case RemoteMessageTypes.DebuggerCommandRequest:
                    Execute(envelope);
                    break;
            }
        }

        public void Tick()
        {
        }

        public void Dispose()
        {
            _service?.Dispose();
            _service = null;
            _context = null;
        }

        private void SendHierarchy(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(
                    envelope,
                    out RemoteDebuggerHierarchyRequest request,
                    out _))
                request = new RemoteDebuggerHierarchyRequest();

            if (request.ForceRefresh)
                _service.RefreshHierarchy();

            _context.Sender.Send(
                RemoteMessageTypes.DebuggerHierarchyResponse,
                envelope.RequestId,
                RuntimeDebuggerProtocolMapper.ToRemote(_service.GetHierarchySnapshot()));
        }

        private void Inspect(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(
                    envelope,
                    out RemoteDebuggerInspectRequest request,
                    out string error))
            {
                SendInspectError(envelope.RequestId, error);
                return;
            }

            RuntimeObjectDetails details = _service.InspectObject(new RuntimeObjectId(request.ObjectId));
            _context.Sender.Send(
                RemoteMessageTypes.DebuggerInspectResponse,
                envelope.RequestId,
                new RemoteDebuggerInspectResponse
                {
                    Found = details != null,
                    Error = details == null ? "The runtime object no longer exists." : string.Empty,
                    Details = RuntimeDebuggerProtocolMapper.ToRemote(details)
                });
        }

        private void Execute(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(
                    envelope,
                    out RemoteDebuggerCommandRequest request,
                    out string error))
            {
                SendCommandResult(envelope.RequestId, RuntimeCommandResult.Fail(error));
                return;
            }

            RuntimeDebuggerCommand command;
            switch (request.Kind)
            {
                case RemoteDebuggerCommandKind.SetGameObjectActive:
                    command = new SetGameObjectActiveCommand
                    {
                        ObjectId = new RuntimeObjectId(request.ObjectId),
                        Active = request.BooleanValue
                    };
                    break;
                case RemoteDebuggerCommandKind.SetComponentEnabled:
                    command = new SetComponentEnabledCommand
                    {
                        ComponentId = new RuntimeObjectId(request.ComponentId),
                        Enabled = request.BooleanValue
                    };
                    break;
                case RemoteDebuggerCommandKind.SetMemberValue:
                    command = new SetMemberValueCommand
                    {
                        ComponentId = new RuntimeObjectId(request.ComponentId),
                        MemberName = request.MemberName,
                        Value = request.Value
                    };
                    break;
                case RemoteDebuggerCommandKind.SetShaderProperty:
                    command = new SetRuntimeShaderPropertyCommand
                    {
                        RendererId = new RuntimeObjectId(request.RendererId),
                        MaterialIndex = request.MaterialIndex,
                        PropertyId = request.PropertyId,
                        Scope = (RuntimeMaterialEditScope)request.MaterialScope,
                        Value = request.Value
                    };
                    break;
                case RemoteDebuggerCommandKind.RestoreShaderProperty:
                    command = new RestoreRuntimeShaderPropertyCommand
                    {
                        RendererId = new RuntimeObjectId(request.RendererId),
                        MaterialIndex = request.MaterialIndex,
                        PropertyId = request.PropertyId,
                        Scope = (RuntimeMaterialEditScope)request.MaterialScope
                    };
                    break;
                case RemoteDebuggerCommandKind.RestoreMaterial:
                    command = new RestoreRuntimeMaterialCommand
                    {
                        RendererId = new RuntimeObjectId(request.RendererId),
                        MaterialIndex = request.MaterialIndex,
                        Scope = (RuntimeMaterialEditScope)request.MaterialScope
                    };
                    break;
                default:
                    SendCommandResult(
                        envelope.RequestId,
                        RuntimeCommandResult.Fail("The remote debugger command is not supported."));
                    return;
            }

            SendCommandResult(envelope.RequestId, _service.Execute(command));
        }

        private void SendUnavailable(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteMessageTypes.DebuggerInspectRequest)
            {
                SendInspectError(envelope.RequestId, "The remote Runtime Debugger is disabled.");
                return;
            }

            if (envelope.MessageType == RemoteMessageTypes.DebuggerCommandRequest)
            {
                SendCommandResult(
                    envelope.RequestId,
                    RuntimeCommandResult.Fail("The remote Runtime Debugger is disabled."));
                return;
            }

            _context.Sender.Send(
                RemoteMessageTypes.DebuggerHierarchyResponse,
                envelope.RequestId,
                new RemoteDebuggerHierarchyResponse());
        }

        private void SendInspectError(long requestId, string error)
        {
            _context.Sender.Send(
                RemoteMessageTypes.DebuggerInspectResponse,
                requestId,
                new RemoteDebuggerInspectResponse { Error = error });
        }

        private void SendCommandResult(long requestId, RuntimeCommandResult result)
        {
            _context.Sender.Send(
                RemoteMessageTypes.DebuggerCommandResponse,
                requestId,
                new RemoteDebuggerCommandResponse
                {
                    Success = result?.Success == true,
                    Message = result?.Message ?? "The runtime command did not return a result."
                });
        }
    }
}

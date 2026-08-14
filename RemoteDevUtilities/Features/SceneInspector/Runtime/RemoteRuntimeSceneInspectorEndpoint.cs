using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector.Capture;
using SAS.Utilities.RuntimeSceneInspector;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine.Scripting;
using RemoteMessageTypes = SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.RemoteSceneInspectorMessageTypes;

namespace SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector
{
    [Preserve]
    [RuntimeRemoteEndpoint("runtime-scene-inspector", 400)]
    internal sealed class RemoteRuntimeSceneInspectorEndpoint : IRuntimeRemoteEndpoint, IRuntimeRemoteSessionListener
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.SceneInspectorHierarchyRequest,
            RemoteMessageTypes.SceneInspectorInspectRequest,
            RemoteMessageTypes.SceneInspectorCommandRequest,
            RemoteMessageTypes.SceneInspectorCaptureRequest,
            RemoteMessageTypes.SceneInspectorPickRequest
        };

        private RuntimeRemoteEndpointContext _context;
        private RuntimeSceneInspectorService _service;
        private RemoteRuntimeSceneCaptureFeature _captureFeature;

        public IEnumerable<string> MessageTypes => SupportedMessages;

        public void Initialize(RuntimeRemoteEndpointContext context)
        {
            _context = context;
            RuntimeSceneInspectorSettings settings = null;
            if (context.Settings.AllowRuntimeSceneInspector)
            {
                settings = RuntimeSceneInspectorSettings.LoadOrCreateDefaults();
                _service = new RuntimeSceneInspectorService(settings);
            }

            _captureFeature = new RemoteRuntimeSceneCaptureFeature(context, _service, settings);
        }

        public void Handle(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteMessageTypes.SceneInspectorCaptureRequest)
            {
                _captureFeature.Capture(envelope);
                return;
            }

            if (envelope.MessageType == RemoteMessageTypes.SceneInspectorPickRequest)
            {
                _captureFeature.Pick(envelope);
                return;
            }

            if (_service == null)
            {
                SendUnavailable(envelope);
                return;
            }

            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.SceneInspectorHierarchyRequest:
                    SendHierarchy(envelope);
                    break;
                case RemoteMessageTypes.SceneInspectorInspectRequest:
                    Inspect(envelope);
                    break;
                case RemoteMessageTypes.SceneInspectorCommandRequest:
                    Execute(envelope);
                    break;
            }
        }

        public void Tick()
        {
        }

        public void Dispose()
        {
            _captureFeature?.Dispose();
            _captureFeature = null;
            _service?.Dispose();
            _service = null;
            _context = null;
        }

        public void OnRemoteSessionStateChanged(bool active) => _captureFeature?.OnSessionStateChanged(active);

        private void SendHierarchy(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteSceneInspectorHierarchyRequest request, out _))
                request = new RemoteSceneInspectorHierarchyRequest();

            if (request.ForceRefresh)
                _service.RefreshHierarchy();

            _context.Sender.Send(RemoteMessageTypes.SceneInspectorHierarchyResponse, envelope.RequestId,
                RuntimeSceneInspectorProtocolMapper.ToRemote(_service.GetHierarchySnapshot()));
        }

        private void Inspect(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteSceneInspectorInspectRequest request, out string error))
            {
                SendInspectError(envelope.RequestId, error);
                return;
            }

            RuntimeObjectDetails details = _service.InspectObject(new RuntimeObjectId(request.ObjectId));
            _context.Sender.Send(RemoteMessageTypes.SceneInspectorInspectResponse, envelope.RequestId,
                new RemoteSceneInspectorInspectResponse
                {
                    Found = details != null,
                    Error = details == null ? "The runtime object no longer exists." : string.Empty,
                    Details = RuntimeSceneInspectorProtocolMapper.ToRemote(details)
                });
        }

        private void Execute(RemoteEnvelope envelope)
        {
            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteSceneInspectorCommandRequest request, out string error))
            {
                SendCommandResult(envelope.RequestId, RuntimeCommandResult.Fail(error));
                return;
            }

            RuntimeSceneInspectorCommand command;
            switch (request.Kind)
            {
                case RemoteSceneInspectorCommandKind.SetGameObjectActive:
                    command = new SetGameObjectActiveCommand
                    {
                        ObjectId = new RuntimeObjectId(request.ObjectId),
                        Active = request.BooleanValue
                    };
                    break;
                case RemoteSceneInspectorCommandKind.SetComponentEnabled:
                    command = new SetComponentEnabledCommand
                    {
                        ComponentId = new RuntimeObjectId(request.ComponentId),
                        Enabled = request.BooleanValue
                    };
                    break;
                case RemoteSceneInspectorCommandKind.SetMemberValue:
                    command = new SetMemberValueCommand
                    {
                        ComponentId = new RuntimeObjectId(request.ComponentId),
                        MemberName = request.MemberName,
                        Value = request.Value
                    };
                    break;
                case RemoteSceneInspectorCommandKind.SetShaderProperty:
                    command = new SetRuntimeShaderPropertyCommand
                    {
                        RendererId = new RuntimeObjectId(request.RendererId),
                        MaterialIndex = request.MaterialIndex,
                        PropertyId = request.PropertyId,
                        Scope = (RuntimeMaterialEditScope)request.MaterialScope,
                        Value = request.Value
                    };
                    break;
                case RemoteSceneInspectorCommandKind.RestoreShaderProperty:
                    command = new RestoreRuntimeShaderPropertyCommand
                    {
                        RendererId = new RuntimeObjectId(request.RendererId),
                        MaterialIndex = request.MaterialIndex,
                        PropertyId = request.PropertyId,
                        Scope = (RuntimeMaterialEditScope)request.MaterialScope
                    };
                    break;
                case RemoteSceneInspectorCommandKind.RestoreMaterial:
                    command = new RestoreRuntimeMaterialCommand
                    {
                        RendererId = new RuntimeObjectId(request.RendererId),
                        MaterialIndex = request.MaterialIndex,
                        Scope = (RuntimeMaterialEditScope)request.MaterialScope
                    };
                    break;
                default:
                    SendCommandResult(envelope.RequestId,
                        RuntimeCommandResult.Fail("The remote scene inspector command is not supported."));
                    return;
            }

            SendCommandResult(envelope.RequestId, _service.Execute(command));
        }

        private void SendUnavailable(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteMessageTypes.SceneInspectorInspectRequest)
            {
                SendInspectError(envelope.RequestId, "The remote Runtime Scene Inspector is disabled.");
                return;
            }

            if (envelope.MessageType == RemoteMessageTypes.SceneInspectorCommandRequest)
            {
                SendCommandResult(envelope.RequestId,
                    RuntimeCommandResult.Fail("The remote Runtime Scene Inspector is disabled."));
                return;
            }

            _context.Sender.Send(RemoteMessageTypes.SceneInspectorHierarchyResponse, envelope.RequestId,
                new RemoteSceneInspectorHierarchyResponse());
        }

        private void SendInspectError(long requestId, string error)
        {
            _context.Sender.Send(RemoteMessageTypes.SceneInspectorInspectResponse, requestId,
                new RemoteSceneInspectorInspectResponse { Error = error });
        }

        private void SendCommandResult(long requestId, RuntimeCommandResult result)
        {
            _context.Sender.Send(RemoteMessageTypes.SceneInspectorCommandResponse, requestId,
                new RemoteSceneInspectorCommandResponse
                {
                    Success = result?.Success == true,
                    Message = result?.Message ?? "The runtime command did not return a result."
                });
        }
    }
}

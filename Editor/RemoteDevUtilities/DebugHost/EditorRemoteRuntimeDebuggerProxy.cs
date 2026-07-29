using System;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeDebugger;
using SAS.Utilities.RuntimeDebugger.Core;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal sealed class EditorRemoteRuntimeDebuggerProxy : IRuntimeDebugger
    {
        private readonly RemoteDevUtilitiesClient _client;
        private long _pendingInspectionId;

        public EditorRemoteRuntimeDebuggerProxy(RemoteDevUtilitiesClient client)
        {
            _client = client;
        }

        public RuntimeHierarchySnapshot GetHierarchySnapshot() =>
            RemoteRuntimeDebuggerModelMapper.ToRuntime(_client.RuntimeDebugger.Hierarchy);

        public RuntimeObjectDetails InspectObject(RuntimeObjectId objectId)
        {
            RemoteDebuggerInspectResponse inspection = _client.RuntimeDebugger.Inspection;
            if (_client.RuntimeDebugger.InspectionObjectId == objectId.Value &&
                inspection != null)
            {
                _pendingInspectionId = 0;
                if (inspection.Found && inspection.Details?.Id == objectId.Value)
                    return RemoteRuntimeDebuggerModelMapper.ToRuntime(inspection.Details);

                return CreatePendingDetails(
                    objectId,
                    inspection.Error ?? "The remote object is unavailable.");
            }

            if (_pendingInspectionId != objectId.Value)
            {
                _pendingInspectionId = objectId.Value;
                _client.RuntimeDebugger.Inspect(objectId.Value);
            }

            return CreatePendingDetails(objectId, "Loading remote object...");
        }

        private static RuntimeObjectDetails CreatePendingDetails(
            RuntimeObjectId objectId,
            string name)
        {
            return new RuntimeObjectDetails
            {
                Id = objectId,
                Name = name,
                Tag = string.Empty,
                Components = Array.Empty<RuntimeComponentDescriptor>()
            };
        }

        public RuntimeCommandResult Execute(RuntimeDebuggerCommand command)
        {
            if (!TryMap(command, out RemoteDebuggerCommandRequest request))
                return RuntimeCommandResult.Fail("This debugger command is not supported remotely.");

            _client.RuntimeDebugger.Execute(request);
            return RuntimeCommandResult.Ok("Command sent to the runtime Player.");
        }

        public void RefreshHierarchy() => _client.RuntimeDebugger.RequestHierarchy(true);

        private static bool TryMap(
            RuntimeDebuggerCommand command,
            out RemoteDebuggerCommandRequest request)
        {
            request = new RemoteDebuggerCommandRequest();
            switch (command)
            {
                case SetGameObjectActiveCommand active:
                    request.Kind = RemoteDebuggerCommandKind.SetGameObjectActive;
                    request.ObjectId = active.ObjectId.Value;
                    request.BooleanValue = active.Active;
                    return true;
                case SetComponentEnabledCommand enabled:
                    request.Kind = RemoteDebuggerCommandKind.SetComponentEnabled;
                    request.ComponentId = enabled.ComponentId.Value;
                    request.BooleanValue = enabled.Enabled;
                    return true;
                case SetMemberValueCommand member:
                    request.Kind = RemoteDebuggerCommandKind.SetMemberValue;
                    request.ComponentId = member.ComponentId.Value;
                    request.MemberName = member.MemberName;
                    request.Value = member.Value;
                    return true;
                case SetRuntimeShaderPropertyCommand shader:
                    request.Kind = RemoteDebuggerCommandKind.SetShaderProperty;
                    request.RendererId = shader.RendererId.Value;
                    request.MaterialIndex = shader.MaterialIndex;
                    request.PropertyId = shader.PropertyId;
                    request.MaterialScope = (int)shader.Scope;
                    request.Value = shader.Value;
                    return true;
                case RestoreRuntimeShaderPropertyCommand restoreProperty:
                    request.Kind = RemoteDebuggerCommandKind.RestoreShaderProperty;
                    request.RendererId = restoreProperty.RendererId.Value;
                    request.MaterialIndex = restoreProperty.MaterialIndex;
                    request.PropertyId = restoreProperty.PropertyId;
                    request.MaterialScope = (int)restoreProperty.Scope;
                    return true;
                case RestoreRuntimeMaterialCommand restoreMaterial:
                    request.Kind = RemoteDebuggerCommandKind.RestoreMaterial;
                    request.RendererId = restoreMaterial.RendererId.Value;
                    request.MaterialIndex = restoreMaterial.MaterialIndex;
                    request.MaterialScope = (int)restoreMaterial.Scope;
                    return true;
                default:
                    return false;
            }
        }
    }
}

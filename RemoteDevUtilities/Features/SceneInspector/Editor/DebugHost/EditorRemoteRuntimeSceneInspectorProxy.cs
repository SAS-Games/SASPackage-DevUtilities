using System;
using HP.Utilities.RemoteDevUtilities.Editor.Client;
using HP.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector;
using HP.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using HP.Utilities.RuntimeSceneInspector.Core;

namespace HP.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal sealed class EditorRemoteRuntimeSceneInspectorProxy : IRuntimeSceneInspector
    {
        private readonly RemoteRuntimeSceneInspectorClient _inspector;
        private long _pendingInspectionId;

        public EditorRemoteRuntimeSceneInspectorProxy(RemoteDevUtilitiesClient client)
        {
            _inspector = client.GetRequiredFeature<RemoteRuntimeSceneInspectorClient>();
        }

        public RuntimeHierarchySnapshot GetHierarchySnapshot() => RemoteRuntimeSceneInspectorModelMapper.ToRuntime(_inspector.Hierarchy);

        public RuntimeObjectDetails InspectObject(RuntimeObjectId objectId)
        {
            RemoteSceneInspectorInspectResponse inspection = _inspector.Inspection;
            if (_inspector.InspectionObjectId == objectId.Value && inspection != null)
            {
                _pendingInspectionId = 0;
                if (inspection.Found && inspection.Details?.Id == objectId.Value)
                    return RemoteRuntimeSceneInspectorModelMapper.ToRuntime(inspection.Details);

                return CreatePendingDetails(objectId, inspection.Error ?? "The remote object is unavailable.");
            }

            if (_pendingInspectionId != objectId.Value)
            {
                _pendingInspectionId = objectId.Value;
                _inspector.Inspect(objectId.Value);
            }

            return CreatePendingDetails(objectId, "Loading remote object...");
        }

        private static RuntimeObjectDetails CreatePendingDetails(RuntimeObjectId objectId, string name)
        {
            return new RuntimeObjectDetails
            {
                Id = objectId,
                Name = name,
                Tag = string.Empty,
                Components = Array.Empty<RuntimeComponentDescriptor>()
            };
        }

        public RuntimeCommandResult Execute(RuntimeSceneInspectorCommand command)
        {
            if (!TryMap(command, out RemoteSceneInspectorCommandRequest request))
                return RuntimeCommandResult.Fail("This Scene Inspector command is not supported remotely.");

            _inspector.Execute(request);
            return RuntimeCommandResult.Ok("Command sent to the runtime Player.");
        }

        public void RefreshHierarchy() => _inspector.RequestHierarchy(true);

        private static bool TryMap(RuntimeSceneInspectorCommand command, out RemoteSceneInspectorCommandRequest request)
        {
            request = new RemoteSceneInspectorCommandRequest();
            switch (command)
            {
                case SetGameObjectActiveCommand active:
                    request.Kind = RemoteSceneInspectorCommandKind.SetGameObjectActive;
                    request.ObjectId = active.ObjectId.Value;
                    request.BooleanValue = active.Active;
                    return true;
                case SetComponentEnabledCommand enabled:
                    request.Kind = RemoteSceneInspectorCommandKind.SetComponentEnabled;
                    request.ComponentId = enabled.ComponentId.Value;
                    request.BooleanValue = enabled.Enabled;
                    return true;
                case SetMemberValueCommand member:
                    request.Kind = RemoteSceneInspectorCommandKind.SetMemberValue;
                    request.ComponentId = member.ComponentId.Value;
                    request.MemberName = member.MemberName;
                    request.Value = member.Value;
                    return true;
                case SetRuntimeShaderPropertyCommand shader:
                    request.Kind = RemoteSceneInspectorCommandKind.SetShaderProperty;
                    request.RendererId = shader.RendererId.Value;
                    request.MaterialIndex = shader.MaterialIndex;
                    request.PropertyId = shader.PropertyId;
                    request.MaterialScope = (int)shader.Scope;
                    request.Value = shader.Value;
                    return true;
                case RestoreRuntimeShaderPropertyCommand restoreProperty:
                    request.Kind = RemoteSceneInspectorCommandKind.RestoreShaderProperty;
                    request.RendererId = restoreProperty.RendererId.Value;
                    request.MaterialIndex = restoreProperty.MaterialIndex;
                    request.PropertyId = restoreProperty.PropertyId;
                    request.MaterialScope = (int)restoreProperty.Scope;
                    return true;
                case RestoreRuntimeMaterialCommand restoreMaterial:
                    request.Kind = RemoteSceneInspectorCommandKind.RestoreMaterial;
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

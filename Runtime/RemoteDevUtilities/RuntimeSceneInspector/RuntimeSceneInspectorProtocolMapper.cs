using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RuntimeSceneInspector.Core;

namespace SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector
{
    internal static class RuntimeSceneInspectorProtocolMapper
    {
        public static RemoteSceneInspectorHierarchyResponse ToRemote(RuntimeHierarchySnapshot snapshot)
        {
            if (snapshot == null)
                return new RemoteSceneInspectorHierarchyResponse();

            IReadOnlyList<RuntimeHierarchyEntry> source =
                snapshot.Entries ?? Array.Empty<RuntimeHierarchyEntry>();
            var entries = new RemoteHierarchyEntry[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                RuntimeHierarchyEntry entry = source[i];
                entries[i] = new RemoteHierarchyEntry
                {
                    Id = entry.Id.Value,
                    ParentId = entry.ParentId.Value,
                    SceneId = entry.SceneId.Value,
                    Kind = (int)entry.Kind,
                    Name = entry.Name,
                    ActiveSelf = entry.ActiveSelf,
                    ActiveInHierarchy = entry.ActiveInHierarchy,
                    ComponentTypeNames = entry.ComponentTypeNames ?? Array.Empty<string>()
                };
            }

            return new RemoteSceneInspectorHierarchyResponse
            {
                Revision = snapshot.Revision,
                Entries = entries
            };
        }

        public static RemoteObjectDetails ToRemote(RuntimeObjectDetails details)
        {
            if (details == null)
                return null;

            IReadOnlyList<RuntimeComponentDescriptor> sourceComponents =
                details.Components ?? Array.Empty<RuntimeComponentDescriptor>();
            var components = new RemoteComponentDescriptor[sourceComponents.Count];
            for (int i = 0; i < sourceComponents.Count; i++)
                components[i] = ToRemote(sourceComponents[i]);

            return new RemoteObjectDetails
            {
                Id = details.Id.Value,
                Name = details.Name,
                Active = details.Active,
                Tag = details.Tag,
                Layer = details.Layer,
                Components = components,
                MaterialsAndShaders = ToRemote(details.MaterialsAndShaders)
            };
        }

        private static RemoteComponentDescriptor ToRemote(RuntimeComponentDescriptor component)
        {
            IReadOnlyList<RuntimeMemberDescriptor> sourceMembers =
                component.Members ?? Array.Empty<RuntimeMemberDescriptor>();
            var members = new RemoteMemberDescriptor[sourceMembers.Count];
            for (int i = 0; i < sourceMembers.Count; i++)
            {
                RuntimeMemberDescriptor member = sourceMembers[i];
                members[i] = new RemoteMemberDescriptor
                {
                    Name = member.Name,
                    DisplayName = member.DisplayName,
                    TypeName = member.TypeName,
                    Value = member.Value,
                    ReadOnly = member.ReadOnly,
                    Error = member.Error
                };
            }

            return new RemoteComponentDescriptor
            {
                Id = component.Id.Value,
                TypeName = component.TypeName,
                HasEnabledState = component.HasEnabledState,
                Enabled = component.Enabled,
                Missing = component.Missing,
                StatusMessage = component.StatusMessage,
                Members = members
            };
        }

        private static RemoteMaterialShaderSection ToRemote(RuntimeMaterialShaderSection section)
        {
            if (section == null)
                return null;

            IReadOnlyList<RuntimeRendererMaterialDescriptor> sourceRenderers =
                section.Renderers ?? Array.Empty<RuntimeRendererMaterialDescriptor>();
            var renderers = new RemoteRendererMaterialDescriptor[sourceRenderers.Count];
            for (int i = 0; i < sourceRenderers.Count; i++)
            {
                RuntimeRendererMaterialDescriptor renderer = sourceRenderers[i];
                IReadOnlyList<RuntimeMaterialSlotDescriptor> sourceSlots =
                    renderer.MaterialSlots ?? Array.Empty<RuntimeMaterialSlotDescriptor>();
                var slots = new RemoteMaterialSlotDescriptor[sourceSlots.Count];
                for (int slotIndex = 0; slotIndex < sourceSlots.Count; slotIndex++)
                    slots[slotIndex] = ToRemote(sourceSlots[slotIndex]);

                renderers[i] = new RemoteRendererMaterialDescriptor
                {
                    RendererId = renderer.RendererId.Value,
                    RendererName = renderer.RendererName,
                    RendererType = renderer.RendererType,
                    MaterialSlots = slots
                };
            }

            return new RemoteMaterialShaderSection
            {
                DisplayName = section.DisplayName,
                Renderers = renderers
            };
        }

        private static RemoteMaterialSlotDescriptor ToRemote(RuntimeMaterialSlotDescriptor slot)
        {
            IReadOnlyList<RuntimeShaderPropertyView> sourceProperties =
                slot.Properties ?? Array.Empty<RuntimeShaderPropertyView>();
            var properties = new RemoteShaderPropertyView[sourceProperties.Count];
            for (int i = 0; i < sourceProperties.Count; i++)
            {
                RuntimeShaderPropertyView view = sourceProperties[i];
                RuntimeShaderPropertyDescriptor property = view.Property;
                properties[i] = new RemoteShaderPropertyView
                {
                    Index = property?.Index ?? -1,
                    PropertyId = property?.PropertyId ?? 0,
                    Name = property?.Name,
                    DisplayName = property?.DisplayName,
                    Type = property == null ? (int)RuntimeShaderPropertyType.Unsupported : (int)property.Type,
                    Flags = property == null ? 0 : (int)property.Flags,
                    RangeMinimum = property?.RangeMinimum ?? 0f,
                    RangeMaximum = property?.RangeMaximum ?? 0f,
                    DefaultFloatValue = property?.DefaultFloatValue ?? 0f,
                    DefaultVectorValue = property?.DefaultVectorValue ?? default,
                    DefaultTextureName = property?.DefaultTextureName,
                    Value = view.Value,
                    ValueSource = view.ValueSource,
                    ReadOnly = view.ReadOnly,
                    HasInspectorOverride = view.HasInspectorOverride
                };
            }

            return new RemoteMaterialSlotDescriptor
            {
                MaterialIndex = slot.MaterialIndex,
                MaterialName = slot.MaterialName,
                MaterialInstanceId = slot.MaterialInstanceId,
                ShaderName = slot.ShaderName,
                RenderQueue = slot.RenderQueue,
                EnableInstancing = slot.EnableInstancing,
                MissingMaterial = slot.MissingMaterial,
                MissingShader = slot.MissingShader,
                IsInspectorMaterialInstance = slot.IsInspectorMaterialInstance,
                TotalPropertyCount = slot.TotalPropertyCount,
                PropertyLimitReached = slot.PropertyLimitReached,
                Properties = properties
            };
        }
    }
}

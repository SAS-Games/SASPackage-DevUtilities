using System;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RuntimeSceneInspector.Core;
using UnityEngine.Rendering;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal static class RemoteRuntimeSceneInspectorModelMapper
    {
        public static RuntimeHierarchySnapshot ToRuntime(RemoteSceneInspectorHierarchyResponse response)
        {
            RemoteHierarchyEntry[] source = response?.Entries ?? Array.Empty<RemoteHierarchyEntry>();
            var entries = new RuntimeHierarchyEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                RemoteHierarchyEntry entry = source[i];
                entries[i] = new RuntimeHierarchyEntry
                {
                    Id = new RuntimeObjectId(entry.Id),
                    ParentId = new RuntimeObjectId(entry.ParentId),
                    SceneId = new RuntimeObjectId(entry.SceneId),
                    Kind = (RuntimeHierarchyKind)entry.Kind,
                    Name = entry.Name,
                    ActiveSelf = entry.ActiveSelf,
                    ActiveInHierarchy = entry.ActiveInHierarchy,
                    ComponentTypeNames = entry.ComponentTypeNames ?? Array.Empty<string>()
                };
            }

            return new RuntimeHierarchySnapshot
            {
                Revision = response?.Revision ?? 0,
                Entries = entries
            };
        }

        public static RuntimeObjectDetails ToRuntime(RemoteObjectDetails details)
        {
            if (details == null)
                return null;

            RemoteComponentDescriptor[] sourceComponents = details.Components ?? Array.Empty<RemoteComponentDescriptor>();
            var components = new RuntimeComponentDescriptor[sourceComponents.Length];
            for (int i = 0; i < sourceComponents.Length; i++)
                components[i] = ToRuntime(sourceComponents[i]);

            return new RuntimeObjectDetails
            {
                Id = new RuntimeObjectId(details.Id),
                Name = details.Name,
                Active = details.Active,
                Tag = details.Tag,
                Layer = details.Layer,
                Components = components,
                MaterialsAndShaders = ToRuntime(details.MaterialsAndShaders)
            };
        }

        private static RuntimeComponentDescriptor ToRuntime(RemoteComponentDescriptor component)
        {
            RemoteMemberDescriptor[] sourceMembers = component.Members ?? Array.Empty<RemoteMemberDescriptor>();
            var members = new RuntimeMemberDescriptor[sourceMembers.Length];
            for (int i = 0; i < sourceMembers.Length; i++)
            {
                RemoteMemberDescriptor member = sourceMembers[i];
                RemoteInspectorOption[] sourceOptions = member.Options ?? Array.Empty<RemoteInspectorOption>();
                var options = new RuntimeInspectorOption[sourceOptions.Length];
                for (int optionIndex = 0; optionIndex < sourceOptions.Length; optionIndex++)
                {
                    RemoteInspectorOption option = sourceOptions[optionIndex];
                    options[optionIndex] = new RuntimeInspectorOption
                    {
                        Label = option.Label,
                        Value = option.Value,
                        NumericValue = option.NumericValue,
                        HasNumericValue = option.HasNumericValue
                    };
                }
                members[i] = new RuntimeMemberDescriptor
                {
                    Name = member.Name,
                    DisplayName = member.DisplayName,
                    TypeName = member.TypeName,
                    Value = member.Value,
                    ReadOnly = member.ReadOnly,
                    Error = member.Error,
                    ControlKind = (RuntimeInspectorControlKind)member.ControlKind,
                    Capabilities = (RuntimeInspectorMemberCapabilities)member.Capabilities,
                    Options = options,
                    HasRange = member.HasRange,
                    RangeMinimum = member.RangeMinimum,
                    RangeMaximum = member.RangeMaximum
                };
            }

            return new RuntimeComponentDescriptor
            {
                Id = new RuntimeObjectId(component.Id),
                TypeName = component.TypeName,
                HasEnabledState = component.HasEnabledState,
                Enabled = component.Enabled,
                Missing = component.Missing,
                StatusMessage = component.StatusMessage,
                Members = members
            };
        }

        private static RuntimeMaterialShaderSection ToRuntime(RemoteMaterialShaderSection section)
        {
            if (section == null)
                return null;

            RemoteRendererMaterialDescriptor[] sourceRenderers = section.Renderers ?? Array.Empty<RemoteRendererMaterialDescriptor>();
            var renderers = new RuntimeRendererMaterialDescriptor[sourceRenderers.Length];
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                RemoteRendererMaterialDescriptor renderer = sourceRenderers[i];
                RemoteMaterialSlotDescriptor[] sourceSlots = renderer.MaterialSlots ?? Array.Empty<RemoteMaterialSlotDescriptor>();
                var slots = new RuntimeMaterialSlotDescriptor[sourceSlots.Length];
                for (int slotIndex = 0; slotIndex < sourceSlots.Length; slotIndex++)
                    slots[slotIndex] = ToRuntime(sourceSlots[slotIndex]);

                renderers[i] = new RuntimeRendererMaterialDescriptor
                {
                    RendererId = new RuntimeObjectId(renderer.RendererId),
                    RendererName = renderer.RendererName,
                    RendererType = renderer.RendererType,
                    MaterialSlots = slots
                };
            }

            return new RuntimeMaterialShaderSection
            {
                DisplayName = section.DisplayName,
                Renderers = renderers
            };
        }

        private static RuntimeMaterialSlotDescriptor ToRuntime(RemoteMaterialSlotDescriptor slot)
        {
            RemoteShaderPropertyView[] sourceProperties = slot.Properties ?? Array.Empty<RemoteShaderPropertyView>();
            var properties = new RuntimeShaderPropertyView[sourceProperties.Length];
            for (int i = 0; i < sourceProperties.Length; i++)
            {
                RemoteShaderPropertyView property = sourceProperties[i];
                RemoteShaderPropertyScopeView[] sourceScopes =
                    property.Scopes ?? Array.Empty<RemoteShaderPropertyScopeView>();
                var scopes = new RuntimeShaderPropertyScopeView[sourceScopes.Length];
                for (int scopeIndex = 0; scopeIndex < sourceScopes.Length; scopeIndex++)
                {
                    RemoteShaderPropertyScopeView scope = sourceScopes[scopeIndex];
                    scopes[scopeIndex] = new RuntimeShaderPropertyScopeView
                    {
                        Scope = (RuntimeMaterialEditScope)scope.Scope,
                        Value = scope.Value,
                        ValueSource = scope.ValueSource,
                        ReadOnly = scope.ReadOnly,
                        HasInspectorOverride = scope.HasInspectorOverride
                    };
                }
                properties[i] = new RuntimeShaderPropertyView
                {
                    Property = new RuntimeShaderPropertyDescriptor
                    {
                        Index = property.Index,
                        PropertyId = property.PropertyId,
                        Name = property.Name,
                        DisplayName = property.DisplayName,
                        Type = (RuntimeShaderPropertyType)property.Type,
                        Flags = (ShaderPropertyFlags)property.Flags,
                        RangeMinimum = property.RangeMinimum,
                        RangeMaximum = property.RangeMaximum,
                        DefaultFloatValue = property.DefaultFloatValue,
                        DefaultVectorValue = property.DefaultVectorValue,
                        DefaultTextureName = property.DefaultTextureName
                    },
                    Value = property.Value,
                    ValueSource = property.ValueSource,
                    ReadOnly = property.ReadOnly,
                    HasInspectorOverride = property.HasInspectorOverride,
                    Scopes = scopes
                };
            }

            RemoteMaterialScopeState[] sourceSlotScopes =
                slot.Scopes ?? Array.Empty<RemoteMaterialScopeState>();
            var slotScopes = new RuntimeMaterialScopeState[sourceSlotScopes.Length];
            for (int i = 0; i < sourceSlotScopes.Length; i++)
            {
                RemoteMaterialScopeState scope = sourceSlotScopes[i];
                slotScopes[i] = new RuntimeMaterialScopeState
                {
                    Scope = (RuntimeMaterialEditScope)scope.Scope,
                    ReadOnly = scope.ReadOnly,
                    HasInspectorOverrides = scope.HasInspectorOverrides
                };
            }

            return new RuntimeMaterialSlotDescriptor
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
                Properties = properties,
                Scopes = slotScopes
            };
        }
    }
}

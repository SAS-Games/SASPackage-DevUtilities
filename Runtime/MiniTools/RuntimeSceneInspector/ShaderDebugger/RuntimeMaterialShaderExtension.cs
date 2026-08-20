using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    /// <summary>
    /// Adds the in-build material/shader section. The default edit path uses a material property block,
    /// so inspecting or changing a renderer never instantiates a material implicitly.
    /// </summary>
    internal sealed class RuntimeMaterialShaderExtension : IRuntimeSceneInspectorExtension
    {
        private readonly RuntimeSceneInspectorSettings _settings;
        private readonly RuntimeShaderMetadataCache _metadata = new();
        private readonly MaterialPropertyBlock _propertyBlock = new();
        private readonly Dictionary<PropertyKey, PropertyBlockOverrideRecord> _propertyBlockOverrides = new();
        private readonly Dictionary<SlotKey, PropertyBlockSlotRecord> _propertyBlockSlots = new();
        private readonly Dictionary<SlotKey, MaterialInstanceRecord> _materialInstances = new();
        private readonly Dictionary<MaterialPropertyKey, MaterialOverrideRecord> _sharedMaterialOverrides = new();
        private readonly Dictionary<int, GlobalOverrideRecord> _globalOverrides = new();
        private readonly List<SlotKey> _slotKeyBuffer = new();
        private readonly List<PropertyKey> _propertyKeyBuffer = new();
        private readonly List<MaterialPropertyKey> _materialPropertyKeyBuffer = new();
        private readonly List<Renderer> _rendererBuffer = new();

        internal RuntimeMaterialShaderExtension(RuntimeSceneInspectorSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Inspect(GameObject target, RuntimeObjectRegistry registry, RuntimeObjectDetails details)
        {
            if (!_settings.AllowShaderInspection || target == null || details == null)
                return;

            PruneInvalidState();
            _rendererBuffer.Clear();
            target.GetComponents(_rendererBuffer);
            if (_rendererBuffer.Count == 0)
                return;

            var rendererDescriptors = new List<RuntimeRendererMaterialDescriptor>(_rendererBuffer.Count);
            for (int i = 0; i < _rendererBuffer.Count; i++)
            {
                Renderer renderer = _rendererBuffer[i];
                if (renderer == null)
                    continue;

                rendererDescriptors.Add(BuildRenderer(renderer, registry));
            }
            _rendererBuffer.Clear();

            if (rendererDescriptors.Count > 0)
            {
                details.MaterialsAndShaders = new RuntimeMaterialShaderSection
                {
                    Renderers = rendererDescriptors
                };
            }
        }

        public bool TryExecute(RuntimeSceneInspectorCommand command, RuntimeObjectRegistry registry, out RuntimeCommandResult result)
        {
            if (command is SetRuntimeShaderPropertyCommand set)
            {
                result = SetProperty(set, registry);
                return true;
            }

            if (command is RestoreRuntimeShaderPropertyCommand restoreProperty)
            {
                result = RestoreProperty(restoreProperty, registry);
                return true;
            }

            if (command is RestoreRuntimeMaterialCommand restoreMaterial)
            {
                result = RestoreMaterial(restoreMaterial, registry);
                return true;
            }

            result = null;
            return false;
        }

        public void Dispose()
        {
            RestoreAllPropertyBlocks();
            RestoreAllMaterialInstances();
            RestoreAllSharedMaterialValues();
            RestoreAllGlobalValues();
            _metadata.Clear();
        }

        private RuntimeRendererMaterialDescriptor BuildRenderer(Renderer renderer, RuntimeObjectRegistry registry)
        {
            Material[] materials = renderer.sharedMaterials;
            var slots = new List<RuntimeMaterialSlotDescriptor>(materials.Length);
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                slots.Add(BuildSlot(renderer, materialIndex, materials[materialIndex]));

            return new RuntimeRendererMaterialDescriptor
            {
                RendererId = registry.GetOrCreate(renderer),
                RendererName = renderer.name,
                RendererType = renderer.GetType().FullName,
                MaterialSlots = slots
            };
        }

        private RuntimeMaterialSlotDescriptor BuildSlot(Renderer renderer, int materialIndex, Material material)
        {
            var slot = new RuntimeMaterialSlotDescriptor
            {
                MaterialIndex = materialIndex,
                MissingMaterial = material == null,
                Properties = Array.Empty<RuntimeShaderPropertyView>()
            };

            if (material == null)
                return slot;

            Shader shader = material.shader;
            slot.MaterialName = material.name;
            slot.MaterialInstanceId = material.GetInstanceID();
            slot.ShaderName = shader != null ? shader.name : "<missing>";
            slot.RenderQueue = material.renderQueue;
            slot.EnableInstancing = material.enableInstancing;
            slot.MissingShader = shader == null;
            slot.IsInspectorMaterialInstance = IsInspectorInstance(renderer, materialIndex, material);
            if (shader == null)
                return slot;

            RuntimeShaderDescriptor shaderDescriptor = _metadata.GetOrCreate(shader);
            slot.TotalPropertyCount = shaderDescriptor.Properties.Count;
            var properties = new List<RuntimeShaderPropertyView>(Mathf.Min(shaderDescriptor.Properties.Count, _settings.MaxVisibleShaderProperties));

            int rendererInstanceId = renderer.GetInstanceID();
            var slotKey = new SlotKey(rendererInstanceId, materialIndex);
            _materialInstances.TryGetValue(slotKey, out MaterialInstanceRecord instanceRecord);
            Material sharedMaterial = instanceRecord != null &&
                                      instanceRecord.InstanceMaterial == material &&
                                      instanceRecord.OriginalMaterial != null
                ? instanceRecord.OriginalMaterial
                : material;

            _propertyBlock.Clear();
            renderer.GetPropertyBlock(_propertyBlock, materialIndex);
            slot.Scopes = BuildScopeStates(slotKey, sharedMaterial, shaderDescriptor);
            int included = 0;
            foreach (RuntimeShaderPropertyDescriptor property in shaderDescriptor.Properties)
            {
                if (property.IsHidden && !_settings.ShowHiddenShaderProperties)
                    continue;
                if (included >= _settings.MaxVisibleShaderProperties)
                {
                    slot.PropertyLimitReached = true;
                    break;
                }

                included++;
                properties.Add(BuildPropertyView(renderer, rendererInstanceId, materialIndex,
                    material, sharedMaterial, instanceRecord, property));
            }

            slot.Properties = properties;
            return slot;
        }

        private RuntimeShaderPropertyView BuildPropertyView(Renderer renderer, int rendererInstanceId,
            int materialIndex, Material assignedMaterial, Material sharedMaterial,
            MaterialInstanceRecord instanceRecord, RuntimeShaderPropertyDescriptor property)
        {
            var scopes = new RuntimeShaderPropertyScopeView[4];
            scopes[(int)RuntimeMaterialEditScope.RendererPropertyBlock] = BuildScopeView(
                RuntimeMaterialEditScope.RendererPropertyBlock, renderer, rendererInstanceId,
                materialIndex, assignedMaterial, sharedMaterial, instanceRecord, property);
            scopes[(int)RuntimeMaterialEditScope.MaterialInstance] = BuildScopeView(
                RuntimeMaterialEditScope.MaterialInstance, renderer, rendererInstanceId,
                materialIndex, assignedMaterial, sharedMaterial, instanceRecord, property);
            scopes[(int)RuntimeMaterialEditScope.SharedMaterial] = BuildScopeView(
                RuntimeMaterialEditScope.SharedMaterial, renderer, rendererInstanceId,
                materialIndex, assignedMaterial, sharedMaterial, instanceRecord, property);
            scopes[(int)RuntimeMaterialEditScope.GlobalShaderProperty] = BuildScopeView(
                RuntimeMaterialEditScope.GlobalShaderProperty, renderer, rendererInstanceId,
                materialIndex, assignedMaterial, sharedMaterial, instanceRecord, property);

            RuntimeShaderPropertyScopeView rendererScope =
                scopes[(int)RuntimeMaterialEditScope.RendererPropertyBlock];
            return new RuntimeShaderPropertyView
            {
                Property = property,
                Scopes = scopes,
                // Keep the renderer-effective fields as the compact/default representation used
                // by existing native integrations. Scope-aware views use Scopes explicitly.
                Value = rendererScope.Value,
                ValueSource = rendererScope.ValueSource,
                ReadOnly = rendererScope.ReadOnly,
                HasInspectorOverride = rendererScope.HasInspectorOverride
            };
        }

        private RuntimeShaderPropertyScopeView BuildScopeView(RuntimeMaterialEditScope scope,
            Renderer renderer, int rendererInstanceId, int materialIndex, Material assignedMaterial,
            Material sharedMaterial, MaterialInstanceRecord instanceRecord,
            RuntimeShaderPropertyDescriptor property)
        {
            var view = new RuntimeShaderPropertyScopeView
            {
                Scope = scope,
                ReadOnly = !CanEdit(property) || !ScopeAllowed(scope)
            };

            try
            {
                RuntimeShaderPropertyValue value;
                switch (scope)
                {
                    case RuntimeMaterialEditScope.RendererPropertyBlock:
                    {
                        var key = new PropertyKey(rendererInstanceId, materialIndex,
                            property.PropertyId);
                        view.HasInspectorOverride = _propertyBlockOverrides.TryGetValue(key,
                            out PropertyBlockOverrideRecord record);
                        if (view.HasInspectorOverride)
                        {
                            value = record.CurrentValue;
                            view.ValueSource = "Inspector renderer override";
                        }
                        else if (_propertyBlock.HasProperty(property.PropertyId))
                        {
                            value = ReadPropertyBlock(_propertyBlock, property);
                            view.ValueSource = "Renderer property block";
                        }
                        else
                        {
                            value = ReadMaterial(assignedMaterial, property);
                            view.ValueSource = "Material";
                            Texture rendererTexture = ResolveRendererTexture(renderer, property);
                            if (rendererTexture != null)
                            {
                                value = RuntimeShaderPropertyValue.Texture(rendererTexture);
                                view.ValueSource = "Sprite renderer";
                            }
                        }
                        break;
                    }
                    case RuntimeMaterialEditScope.MaterialInstance:
                    {
                        value = ReadMaterial(assignedMaterial, property);
                        view.ValueSource = instanceRecord == null
                            ? "Material (instance created on edit)"
                            : "Inspector material instance";
                        if (instanceRecord?.OriginalMaterial != null)
                        {
                            RuntimeShaderPropertyDescriptor originalProperty =
                                FindProperty(instanceRecord.OriginalMaterial, property.PropertyId);
                            if (originalProperty != null)
                            {
                                view.HasInspectorOverride = !ValuesEqual(value,
                                    ReadMaterial(instanceRecord.OriginalMaterial, originalProperty));
                                if (view.HasInspectorOverride)
                                    view.ValueSource = "Inspector material-instance override";
                            }
                        }
                        break;
                    }
                    case RuntimeMaterialEditScope.SharedMaterial:
                    {
                        RuntimeShaderPropertyDescriptor sharedProperty =
                            FindProperty(sharedMaterial, property.PropertyId);
                        if (sharedProperty == null)
                            throw new InvalidOperationException(
                                "The shared material no longer has this shader property.");
                        value = ReadMaterial(sharedMaterial, sharedProperty);
                        var key = new MaterialPropertyKey(sharedMaterial.GetInstanceID(),
                            property.PropertyId);
                        view.HasInspectorOverride = _sharedMaterialOverrides.ContainsKey(key);
                        view.ValueSource = view.HasInspectorOverride
                            ? "Inspector shared-material override"
                            : "Shared material";
                        break;
                    }
                    case RuntimeMaterialEditScope.GlobalShaderProperty:
                        value = ReadGlobal(property);
                        view.HasInspectorOverride =
                            _globalOverrides.ContainsKey(property.PropertyId);
                        view.ValueSource = view.HasInspectorOverride
                            ? "Inspector global override"
                            : "Global shader property";
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported material edit scope.");
                }

                view.Value = Format(value, property);
            }
            catch (Exception ex)
            {
                view.Value = ex.GetType().Name + ": " + ex.Message;
                view.ValueSource = "Unavailable";
                view.ReadOnly = true;
                view.HasInspectorOverride = false;
            }

            return view;
        }

        private IReadOnlyList<RuntimeMaterialScopeState> BuildScopeStates(SlotKey slotKey,
            Material sharedMaterial, RuntimeShaderDescriptor shader)
        {
            return new[]
            {
                new RuntimeMaterialScopeState
                {
                    Scope = RuntimeMaterialEditScope.RendererPropertyBlock,
                    ReadOnly = !ScopeAllowed(RuntimeMaterialEditScope.RendererPropertyBlock),
                    HasInspectorOverrides = HasPropertyBlockOverrides(slotKey)
                },
                new RuntimeMaterialScopeState
                {
                    Scope = RuntimeMaterialEditScope.MaterialInstance,
                    ReadOnly = !ScopeAllowed(RuntimeMaterialEditScope.MaterialInstance),
                    HasInspectorOverrides = _materialInstances.ContainsKey(slotKey)
                },
                new RuntimeMaterialScopeState
                {
                    Scope = RuntimeMaterialEditScope.SharedMaterial,
                    ReadOnly = !ScopeAllowed(RuntimeMaterialEditScope.SharedMaterial),
                    HasInspectorOverrides = HasSharedMaterialOverrides(sharedMaterial)
                },
                new RuntimeMaterialScopeState
                {
                    Scope = RuntimeMaterialEditScope.GlobalShaderProperty,
                    ReadOnly = !ScopeAllowed(RuntimeMaterialEditScope.GlobalShaderProperty),
                    HasInspectorOverrides = HasGlobalOverrides(shader)
                }
            };
        }

        private bool HasPropertyBlockOverrides(SlotKey slotKey)
        {
            foreach (PropertyKey key in _propertyBlockOverrides.Keys)
            {
                if (key.RendererInstanceId == slotKey.RendererInstanceId &&
                    key.MaterialIndex == slotKey.MaterialIndex)
                    return true;
            }
            return false;
        }

        private bool HasSharedMaterialOverrides(Material material)
        {
            if (material == null)
                return false;
            int materialId = material.GetInstanceID();
            foreach (MaterialPropertyKey key in _sharedMaterialOverrides.Keys)
            {
                if (key.MaterialInstanceId == materialId)
                    return true;
            }
            return false;
        }

        private bool HasGlobalOverrides(RuntimeShaderDescriptor shader)
        {
            if (shader?.Properties == null)
                return false;
            for (int i = 0; i < shader.Properties.Count; i++)
            {
                if (_globalOverrides.ContainsKey(shader.Properties[i].PropertyId))
                    return true;
            }
            return false;
        }

        private RuntimeCommandResult SetProperty(SetRuntimeShaderPropertyCommand command, RuntimeObjectRegistry registry)
        {
            if (!_settings.AllowShaderInspection)
                return RuntimeCommandResult.Fail("Shader inspection is disabled.");
            if (!_settings.AllowShaderValueChanges)
                return RuntimeCommandResult.Fail("Shader value changes are disabled.");
            if (!TryResolveRenderer(registry, command.RendererId, out Renderer renderer, out string error))
                return RuntimeCommandResult.Fail(error);
            if (!TryGetScopeMaterial(renderer, command.MaterialIndex, command.Scope, out Material material, out error))
                return RuntimeCommandResult.Fail(error);
            if (!CheckScopePermission(command.Scope, out error))
                return RuntimeCommandResult.Fail(error);

            RuntimeShaderPropertyDescriptor property = FindProperty(material, command.PropertyId);
            if (property == null)
                return RuntimeCommandResult.Fail("The shader property is no longer available.");
            if (!CanEdit(property))
                return RuntimeCommandResult.Fail("This shader property is read-only.");
            if (!TryParse(command.Value, property.Type, out RuntimeShaderPropertyValue value, out error))
                return RuntimeCommandResult.Fail(error);

            switch (command.Scope)
            {
                case RuntimeMaterialEditScope.RendererPropertyBlock:
                    return SetPropertyBlock(renderer, command.MaterialIndex, material, property, value);
                case RuntimeMaterialEditScope.MaterialInstance:
                    return SetMaterialInstance(renderer, command.MaterialIndex, property, value);
                case RuntimeMaterialEditScope.SharedMaterial:
                    return SetSharedMaterial(material, property, value);
                case RuntimeMaterialEditScope.GlobalShaderProperty:
                    return SetGlobalProperty(property, value);
                default:
                    return RuntimeCommandResult.Fail("Unsupported material edit scope.");
            }
        }

        private RuntimeCommandResult RestoreProperty(RestoreRuntimeShaderPropertyCommand command, RuntimeObjectRegistry registry)
        {
            if (!TryResolveRenderer(registry, command.RendererId, out Renderer renderer, out string error))
                return RuntimeCommandResult.Fail(error);
            if (!TryGetScopeMaterial(renderer, command.MaterialIndex, command.Scope, out Material material, out error))
                return RuntimeCommandResult.Fail(error);

            RuntimeShaderPropertyDescriptor property = FindProperty(material, command.PropertyId);
            if (property == null)
                return RuntimeCommandResult.Fail("The shader property is no longer available.");

            switch (command.Scope)
            {
                case RuntimeMaterialEditScope.RendererPropertyBlock:
                    {
                        var key = new PropertyKey(renderer.GetInstanceID(), command.MaterialIndex, property.PropertyId);
                        if (!_propertyBlockOverrides.ContainsKey(key))
                            return RuntimeCommandResult.Fail("The inspector has no renderer override for this property.");
                        _propertyBlockOverrides.Remove(key);
                        RebuildPropertyBlockSlot(renderer, command.MaterialIndex);
                        return RuntimeCommandResult.Ok("Renderer override restored.");
                    }
                case RuntimeMaterialEditScope.MaterialInstance:
                    {
                        var key = new SlotKey(renderer.GetInstanceID(), command.MaterialIndex);
                        if (!_materialInstances.TryGetValue(key, out MaterialInstanceRecord instance) || instance.InstanceMaterial == null || instance.OriginalMaterial == null)
                            return RuntimeCommandResult.Fail("The inspector has no material instance for this slot.");
                        RuntimeShaderPropertyDescriptor originalProperty = FindProperty(instance.OriginalMaterial, property.PropertyId);
                        if (originalProperty == null)
                            return RuntimeCommandResult.Fail("The original material no longer has this property.");
                        WriteMaterial(instance.InstanceMaterial, property, ReadMaterial(instance.OriginalMaterial, originalProperty));
                        return RuntimeCommandResult.Ok("Material-instance value restored.");
                    }
                case RuntimeMaterialEditScope.SharedMaterial:
                    return RestoreSharedProperty(material, property);
                case RuntimeMaterialEditScope.GlobalShaderProperty:
                    return RestoreGlobalProperty(property);
                default:
                    return RuntimeCommandResult.Fail("Unsupported material edit scope.");
            }
        }

        private RuntimeCommandResult RestoreMaterial(RestoreRuntimeMaterialCommand command, RuntimeObjectRegistry registry)
        {
            if (!TryResolveRenderer(registry, command.RendererId, out Renderer renderer, out string error))
                return RuntimeCommandResult.Fail(error);

            switch (command.Scope)
            {
                case RuntimeMaterialEditScope.RendererPropertyBlock:
                    {
                        int restored = RestorePropertyBlockSlot(renderer, command.MaterialIndex);
                        return restored > 0 ? RuntimeCommandResult.Ok($"Restored {restored} renderer override(s).") : RuntimeCommandResult.Fail("The inspector has no renderer overrides for this slot.");
                    }
                case RuntimeMaterialEditScope.MaterialInstance:
                    return RestoreMaterialInstance(renderer, command.MaterialIndex) ? RuntimeCommandResult.Ok("Original material restored.") : RuntimeCommandResult.Fail("The inspector has no material instance for this slot.");
                case RuntimeMaterialEditScope.SharedMaterial:
                    {
                        if (!TryGetScopeMaterial(renderer, command.MaterialIndex, command.Scope, out Material material, out error))
                            return RuntimeCommandResult.Fail(error);
                        int restored = RestoreSharedMaterial(material);
                        return restored > 0 ? RuntimeCommandResult.Ok($"Restored {restored} shared-material value(s).") : RuntimeCommandResult.Fail("The inspector has no shared-material changes for this slot.");
                    }
                case RuntimeMaterialEditScope.GlobalShaderProperty:
                    {
                        if (!TryGetScopeMaterial(renderer, command.MaterialIndex, command.Scope, out Material material, out error))
                            return RuntimeCommandResult.Fail(error);
                        int restored = RestoreGlobalsForShader(material.shader);
                        return restored > 0 ? RuntimeCommandResult.Ok($"Restored {restored} global shader value(s).") : RuntimeCommandResult.Fail("The inspector has no global changes for this shader.");
                    }
                default:
                    return RuntimeCommandResult.Fail("Unsupported material edit scope.");
            }
        }

        private RuntimeCommandResult SetPropertyBlock(Renderer renderer, int materialIndex, Material material, RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            var slotKey = new SlotKey(renderer.GetInstanceID(), materialIndex);
            if (!_propertyBlockSlots.TryGetValue(slotKey, out PropertyBlockSlotRecord slotRecord))
            {
                var originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock, materialIndex);
                slotRecord = new PropertyBlockSlotRecord
                {
                    Renderer = renderer,
                    MaterialIndex = materialIndex,
                    OriginalBlock = originalBlock,
                    OriginalWasEmpty = originalBlock.isEmpty
                };
                _propertyBlockSlots.Add(slotKey, slotRecord);
            }

            var key = new PropertyKey(renderer.GetInstanceID(), materialIndex, property.PropertyId);
            if (!_propertyBlockOverrides.TryGetValue(key, out PropertyBlockOverrideRecord record))
            {
                record = new PropertyBlockOverrideRecord
                {
                    Renderer = renderer,
                    MaterialIndex = materialIndex,
                    Property = property
                };
                _propertyBlockOverrides.Add(key, record);
            }

            ApplyPropertyBlockValue(renderer, materialIndex, property, value);
            record.CurrentValue = value;
            return RuntimeCommandResult.Ok("Applied to this renderer only.");
        }

        private RuntimeCommandResult SetMaterialInstance(Renderer renderer, int materialIndex, RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            if (!TryGetOrCreateMaterialInstance(renderer, materialIndex, out Material instance, out string error))
                return RuntimeCommandResult.Fail(error);

            RuntimeShaderPropertyDescriptor instanceProperty = FindProperty(instance, property.PropertyId);
            if (instanceProperty == null)
                return RuntimeCommandResult.Fail("The material instance no longer has this shader property.");
            WriteMaterial(instance, instanceProperty, value);
            return RuntimeCommandResult.Ok("Applied to an inspector-owned material instance.");
        }

        private RuntimeCommandResult SetSharedMaterial(Material material, RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            var key = new MaterialPropertyKey(material.GetInstanceID(), property.PropertyId);
            if (!_sharedMaterialOverrides.TryGetValue(key, out MaterialOverrideRecord record))
            {
                record = new MaterialOverrideRecord
                {
                    Material = material,
                    Property = property,
                    OriginalValue = ReadMaterial(material, property)
                };
                _sharedMaterialOverrides.Add(key, record);
            }

            WriteMaterial(material, property, value);
            record.CurrentValue = value;
            return RuntimeCommandResult.Ok("Shared material changed; other renderers may be affected.");
        }

        private RuntimeCommandResult SetGlobalProperty(RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            if (!_globalOverrides.TryGetValue(property.PropertyId, out GlobalOverrideRecord record))
            {
                record = new GlobalOverrideRecord
                {
                    Property = property,
                    OriginalValue = ReadGlobal(property)
                };
                _globalOverrides.Add(property.PropertyId, record);
            }
            else if (record.Property.Type != property.Type)
            {
                return RuntimeCommandResult.Fail("This global property was already edited with a different type.");
            }

            WriteGlobal(property, value);
            record.CurrentValue = value;
            return RuntimeCommandResult.Ok("Global shader property changed; multiple shaders may be affected.");
        }

        private bool TryGetOrCreateMaterialInstance(Renderer renderer, int materialIndex, out Material instance, out string error)
        {
            var key = new SlotKey(renderer.GetInstanceID(), materialIndex);
            if (_materialInstances.TryGetValue(key, out MaterialInstanceRecord existing) && existing.InstanceMaterial != null && IsMaterialAssigned(renderer, materialIndex, existing.InstanceMaterial))
            {
                instance = existing.InstanceMaterial;
                error = null;
                return true;
            }

            if (existing != null)
            {
                _materialInstances.Remove(key);
                DestroyOwnedMaterial(existing.InstanceMaterial);
            }

            if (_materialInstances.Count >= _settings.MaxInspectorMaterialInstances)
            {
                instance = null;
                error = "The inspector material-instance limit has been reached.";
                return false;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
            {
                instance = null;
                error = "The material slot is no longer available.";
                return false;
            }

            Material original = materials[materialIndex];
            instance = new Material(original)
            {
                name = original.name + " [Runtime Scene Inspector]",
                hideFlags = HideFlags.DontSave
            };
            materials[materialIndex] = instance;
            renderer.sharedMaterials = materials;
            _materialInstances.Add(key, new MaterialInstanceRecord
            {
                Renderer = renderer,
                MaterialIndex = materialIndex,
                OriginalMaterial = original,
                InstanceMaterial = instance
            });
            error = null;
            return true;
        }

        private bool TryGetScopeMaterial(Renderer renderer, int materialIndex, RuntimeMaterialEditScope scope, out Material material, out string error)
        {
            Material[] materials = renderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length)
            {
                material = null;
                error = "The material slot is no longer available.";
                return false;
            }

            material = materials[materialIndex];
            var key = new SlotKey(renderer.GetInstanceID(), materialIndex);
            if ((scope == RuntimeMaterialEditScope.SharedMaterial || scope == RuntimeMaterialEditScope.GlobalShaderProperty) && _materialInstances.TryGetValue(key, out MaterialInstanceRecord instance) && instance.InstanceMaterial == material && instance.OriginalMaterial != null)
            {
                material = instance.OriginalMaterial;
            }

            if (material == null)
            {
                error = "The material slot is empty.";
                return false;
            }

            if (material.shader == null)
            {
                error = "The material's shader is missing.";
                return false;
            }

            error = null;
            return true;
        }

        private bool CheckScopePermission(RuntimeMaterialEditScope scope, out string error)
        {
            switch (scope)
            {
                case RuntimeMaterialEditScope.RendererPropertyBlock:
                    error = "Renderer property-block changes are disabled.";
                    break;
                case RuntimeMaterialEditScope.MaterialInstance:
                    error = "Material instantiation is disabled.";
                    break;
                case RuntimeMaterialEditScope.SharedMaterial:
                    error = "Shared-material changes are disabled.";
                    break;
                case RuntimeMaterialEditScope.GlobalShaderProperty:
                    error = "Global shader changes are disabled.";
                    break;
                default:
                    error = "Unsupported material edit scope.";
                    return false;
            }

            bool allowed = ScopeAllowed(scope);
            if (allowed)
                error = null;
            return allowed;
        }

        private bool ScopeAllowed(RuntimeMaterialEditScope scope)
        {
            switch (scope)
            {
                case RuntimeMaterialEditScope.RendererPropertyBlock:
                    return _settings.AllowMaterialPropertyBlockChanges;
                case RuntimeMaterialEditScope.MaterialInstance:
                    return _settings.AllowMaterialInstantiation;
                case RuntimeMaterialEditScope.SharedMaterial:
                    return _settings.AllowSharedMaterialChanges;
                case RuntimeMaterialEditScope.GlobalShaderProperty:
                    return _settings.AllowGlobalShaderChanges;
                default:
                    return false;
            }
        }

        private bool CanEdit(RuntimeShaderPropertyDescriptor property) => _settings.AllowShaderValueChanges && property.Type != RuntimeShaderPropertyType.Unsupported && (property.Type != RuntimeShaderPropertyType.Texture || _settings.AllowTextureChanges);

        private RuntimeShaderPropertyDescriptor FindProperty(Material material, int propertyId)
        {
            if (material == null || material.shader == null || !material.HasProperty(propertyId))
                return null;
            RuntimeShaderDescriptor descriptor = _metadata.GetOrCreate(material.shader);
            for (int i = 0; i < descriptor.Properties.Count; i++)
            {
                RuntimeShaderPropertyDescriptor property = descriptor.Properties[i];
                if (property.PropertyId == propertyId)
                    return property;
            }
            return null;
        }

        private static bool TryResolveRenderer(RuntimeObjectRegistry registry, RuntimeObjectId id, out Renderer renderer, out string error)
        {
            if (!registry.TryResolve(id, out renderer) || renderer == null)
            {
                error = "The renderer no longer exists.";
                return false;
            }

            error = null;
            return true;
        }

        private bool IsInspectorInstance(Renderer renderer, int materialIndex, Material material)
        {
            var key = new SlotKey(renderer.GetInstanceID(), materialIndex);
            return _materialInstances.TryGetValue(key, out MaterialInstanceRecord record) && record.InstanceMaterial == material;
        }

        private void ApplyPropertyBlockValue(Renderer renderer, int materialIndex, RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            _propertyBlock.Clear();
            renderer.GetPropertyBlock(_propertyBlock, materialIndex);
            WritePropertyBlock(_propertyBlock, property, value);
            renderer.SetPropertyBlock(_propertyBlock, materialIndex);
        }

        private int RestorePropertyBlockSlot(Renderer renderer, int materialIndex)
        {
            var slotKey = new SlotKey(renderer.GetInstanceID(), materialIndex);
            List<PropertyKey> keys = PropertyBlockKeys(slotKey);
            foreach (PropertyKey key in keys)
                _propertyBlockOverrides.Remove(key);

            if (_propertyBlockSlots.TryGetValue(slotKey, out PropertyBlockSlotRecord slotRecord))
            {
                RestoreOriginalPropertyBlock(slotRecord);
                _propertyBlockSlots.Remove(slotKey);
            }

            return keys.Count;
        }

        private void RebuildPropertyBlockSlot(Renderer renderer, int materialIndex)
        {
            var slotKey = new SlotKey(renderer.GetInstanceID(), materialIndex);
            if (!_propertyBlockSlots.TryGetValue(slotKey, out PropertyBlockSlotRecord slotRecord))
                return;

            RestoreOriginalPropertyBlock(slotRecord);
            List<PropertyKey> remainingKeys = PropertyBlockKeys(slotKey);
            foreach (PropertyKey remainingKey in remainingKeys)
            {
                PropertyBlockOverrideRecord remaining = _propertyBlockOverrides[remainingKey];
                ApplyPropertyBlockValue(renderer, materialIndex, remaining.Property, remaining.CurrentValue);
            }

            if (remainingKeys.Count == 0)
                _propertyBlockSlots.Remove(slotKey);
        }

        private List<PropertyKey> PropertyBlockKeys(SlotKey slotKey)
        {
            _propertyKeyBuffer.Clear();
            foreach (PropertyKey key in _propertyBlockOverrides.Keys)
            {
                if (key.RendererInstanceId == slotKey.RendererInstanceId &&
                    key.MaterialIndex == slotKey.MaterialIndex)
                    _propertyKeyBuffer.Add(key);
            }
            return _propertyKeyBuffer;
        }

        private static void RestoreOriginalPropertyBlock(PropertyBlockSlotRecord record)
        {
            if (record?.Renderer == null)
                return;
            record.Renderer.SetPropertyBlock(record.OriginalWasEmpty ? null : record.OriginalBlock,
                record.MaterialIndex);
        }

        private RuntimeCommandResult RestoreSharedProperty(Material material, RuntimeShaderPropertyDescriptor property)
        {
            var key = new MaterialPropertyKey(material.GetInstanceID(), property.PropertyId);
            if (!_sharedMaterialOverrides.TryGetValue(key, out MaterialOverrideRecord record))
                return RuntimeCommandResult.Fail("The inspector has no shared-material change for this property.");

            if (material != null && ValuesEqual(ReadMaterial(material, property), record.CurrentValue))
                WriteMaterial(material, property, record.OriginalValue);
            _sharedMaterialOverrides.Remove(key);
            return RuntimeCommandResult.Ok("Shared-material value restored.");
        }

        private RuntimeCommandResult RestoreGlobalProperty(RuntimeShaderPropertyDescriptor property)
        {
            if (!_globalOverrides.TryGetValue(property.PropertyId, out GlobalOverrideRecord record))
                return RuntimeCommandResult.Fail("The inspector has no global change for this property.");

            if (ValuesEqual(ReadGlobal(property), record.CurrentValue))
                WriteGlobal(property, record.OriginalValue);
            _globalOverrides.Remove(property.PropertyId);
            return RuntimeCommandResult.Ok("Global shader value restored.");
        }

        private bool RestoreMaterialInstance(Renderer renderer, int materialIndex)
        {
            var key = new SlotKey(renderer.GetInstanceID(), materialIndex);
            if (!_materialInstances.TryGetValue(key, out MaterialInstanceRecord record))
                return false;

            _materialInstances.Remove(key);
            if (renderer != null && record.InstanceMaterial != null && IsMaterialAssigned(renderer, materialIndex, record.InstanceMaterial))
            {
                Material[] materials = renderer.sharedMaterials;
                materials[materialIndex] = record.OriginalMaterial;
                renderer.sharedMaterials = materials;
            }

            DestroyOwnedMaterial(record.InstanceMaterial);
            return true;
        }

        private int RestoreSharedMaterial(Material material)
        {
            if (material == null)
                return 0;
            int materialId = material.GetInstanceID();
            MaterialPropertyKey[] keys = _sharedMaterialOverrides.Keys.Where(key => key.MaterialInstanceId == materialId).ToArray();
            foreach (MaterialPropertyKey key in keys)
            {
                MaterialOverrideRecord record = _sharedMaterialOverrides[key];
                if (record.Material != null && ValuesEqual(ReadMaterial(record.Material, record.Property), record.CurrentValue))
                {
                    WriteMaterial(record.Material, record.Property, record.OriginalValue);
                }

                _sharedMaterialOverrides.Remove(key);
            }

            return keys.Length;
        }

        private int RestoreGlobalsForShader(Shader shader)
        {
            if (shader == null)
                return 0;

            RuntimeShaderDescriptor descriptor = _metadata.GetOrCreate(shader);
            var ids = new HashSet<int>(descriptor.Properties.Select(property => property.PropertyId));
            int[] keys = _globalOverrides.Keys.Where(ids.Contains).ToArray();
            foreach (int propertyId in keys)
            {
                GlobalOverrideRecord record = _globalOverrides[propertyId];
                if (ValuesEqual(ReadGlobal(record.Property), record.CurrentValue))
                    WriteGlobal(record.Property, record.OriginalValue);
                _globalOverrides.Remove(propertyId);
            }

            return keys.Length;
        }

        private void RestoreAllPropertyBlocks()
        {
            foreach (PropertyBlockSlotRecord record in _propertyBlockSlots.Values)
            {
                if (record.Renderer == null)
                    continue;
                try
                {
                    RestoreOriginalPropertyBlock(record);
                }
                catch
                {
                    // The renderer or material layout changed during shutdown.
                }
            }

            _propertyBlockOverrides.Clear();
            _propertyBlockSlots.Clear();
        }

        private void RestoreAllMaterialInstances()
        {
            SlotKey[] keys = _materialInstances.Keys.ToArray();
            foreach (SlotKey key in keys)
            {
                MaterialInstanceRecord record = _materialInstances[key];
                if (record.Renderer != null)
                    RestoreMaterialInstance(record.Renderer, record.MaterialIndex);
                else
                {
                    _materialInstances.Remove(key);
                    DestroyOwnedMaterial(record.InstanceMaterial);
                }
            }
        }

        private void RestoreAllSharedMaterialValues()
        {
            foreach (MaterialOverrideRecord record in _sharedMaterialOverrides.Values)
            {
                if (record.Material == null)
                    continue;
                try
                {
                    if (ValuesEqual(ReadMaterial(record.Material, record.Property), record.CurrentValue))
                        WriteMaterial(record.Material, record.Property, record.OriginalValue);
                }
                catch
                {
                    // The material or shader changed during shutdown.
                }
            }

            _sharedMaterialOverrides.Clear();
        }

        private void RestoreAllGlobalValues()
        {
            foreach (GlobalOverrideRecord record in _globalOverrides.Values)
            {
                try
                {
                    if (ValuesEqual(ReadGlobal(record.Property), record.CurrentValue))
                        WriteGlobal(record.Property, record.OriginalValue);
                }
                catch
                {
                    // The global property's type changed during shutdown.
                }
            }

            _globalOverrides.Clear();
        }

        private void PruneInvalidState()
        {
            _slotKeyBuffer.Clear();
            foreach (KeyValuePair<SlotKey, PropertyBlockSlotRecord> pair in _propertyBlockSlots)
            {
                if (pair.Value.Renderer == null)
                    _slotKeyBuffer.Add(pair.Key);
            }
            foreach (SlotKey key in _slotKeyBuffer)
                _propertyBlockSlots.Remove(key);

            _propertyKeyBuffer.Clear();
            foreach (KeyValuePair<PropertyKey, PropertyBlockOverrideRecord> pair in
                     _propertyBlockOverrides)
            {
                if (pair.Value.Renderer == null || !_propertyBlockSlots.ContainsKey(
                        new SlotKey(pair.Key.RendererInstanceId, pair.Key.MaterialIndex)))
                    _propertyKeyBuffer.Add(pair.Key);
            }
            foreach (PropertyKey key in _propertyKeyBuffer)
                _propertyBlockOverrides.Remove(key);

            _slotKeyBuffer.Clear();
            foreach (KeyValuePair<SlotKey, MaterialInstanceRecord> pair in _materialInstances)
            {
                MaterialInstanceRecord record = pair.Value;
                if (record.Renderer != null && record.InstanceMaterial != null && IsMaterialAssigned(record.Renderer, record.MaterialIndex, record.InstanceMaterial))
                    continue;
                _slotKeyBuffer.Add(pair.Key);
            }
            foreach (SlotKey key in _slotKeyBuffer)
            {
                MaterialInstanceRecord record = _materialInstances[key];
                _materialInstances.Remove(key);
                DestroyOwnedMaterial(record.InstanceMaterial);
            }

            _materialPropertyKeyBuffer.Clear();
            foreach (KeyValuePair<MaterialPropertyKey, MaterialOverrideRecord> pair in
                     _sharedMaterialOverrides)
            {
                if (pair.Value.Material == null)
                    _materialPropertyKeyBuffer.Add(pair.Key);
            }
            foreach (MaterialPropertyKey key in _materialPropertyKeyBuffer)
                _sharedMaterialOverrides.Remove(key);
        }

        private static bool IsMaterialAssigned(Renderer renderer, int materialIndex, Material expected)
        {
            if (renderer == null)
                return false;
            Material[] materials = renderer.sharedMaterials;
            return materialIndex >= 0 && materialIndex < materials.Length && materials[materialIndex] == expected;
        }

        private static void DestroyOwnedMaterial(Material material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
        }

        private static RuntimeShaderPropertyValue ReadMaterial(Material material, RuntimeShaderPropertyDescriptor property)
        {
            switch (property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    return RuntimeShaderPropertyValue.Float(property.Type, material.GetFloat(property.PropertyId));
                case RuntimeShaderPropertyType.Integer:
                    return RuntimeShaderPropertyValue.Integer(material.GetInteger(property.PropertyId));
                case RuntimeShaderPropertyType.Color:
                    return RuntimeShaderPropertyValue.Color(material.GetColor(property.PropertyId));
                case RuntimeShaderPropertyType.Vector:
                    return RuntimeShaderPropertyValue.Vector(material.GetVector(property.PropertyId));
                case RuntimeShaderPropertyType.Texture:
                    return RuntimeShaderPropertyValue.Texture(material.GetTexture(property.PropertyId));
                default:
                    throw new NotSupportedException("Unsupported shader property type.");
            }
        }

        private static void WriteMaterial(Material material, RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            switch (property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    material.SetFloat(property.PropertyId, value.FloatValue);
                    break;
                case RuntimeShaderPropertyType.Integer:
                    material.SetInteger(property.PropertyId, value.IntegerValue);
                    break;
                case RuntimeShaderPropertyType.Color:
                    material.SetColor(property.PropertyId, value.ColorValue);
                    break;
                case RuntimeShaderPropertyType.Vector:
                    material.SetVector(property.PropertyId, value.VectorValue);
                    break;
                case RuntimeShaderPropertyType.Texture:
                    material.SetTexture(property.PropertyId, value.TextureValue);
                    break;
                default:
                    throw new NotSupportedException("Unsupported shader property type.");
            }
        }

        private static RuntimeShaderPropertyValue ReadPropertyBlock(MaterialPropertyBlock block, RuntimeShaderPropertyDescriptor property)
        {
            switch (property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    return RuntimeShaderPropertyValue.Float(property.Type, block.GetFloat(property.PropertyId));
                case RuntimeShaderPropertyType.Integer:
                    return RuntimeShaderPropertyValue.Integer(block.GetInteger(property.PropertyId));
                case RuntimeShaderPropertyType.Color:
                    return RuntimeShaderPropertyValue.Color(block.GetColor(property.PropertyId));
                case RuntimeShaderPropertyType.Vector:
                    return RuntimeShaderPropertyValue.Vector(block.GetVector(property.PropertyId));
                case RuntimeShaderPropertyType.Texture:
                    return RuntimeShaderPropertyValue.Texture(block.GetTexture(property.PropertyId));
                default:
                    throw new NotSupportedException("Unsupported shader property type.");
            }
        }

        private static void WritePropertyBlock(MaterialPropertyBlock block, RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            switch (property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    block.SetFloat(property.PropertyId, value.FloatValue);
                    break;
                case RuntimeShaderPropertyType.Integer:
                    block.SetInteger(property.PropertyId, value.IntegerValue);
                    break;
                case RuntimeShaderPropertyType.Color:
                    block.SetColor(property.PropertyId, value.ColorValue);
                    break;
                case RuntimeShaderPropertyType.Vector:
                    block.SetVector(property.PropertyId, value.VectorValue);
                    break;
                case RuntimeShaderPropertyType.Texture:
                    block.SetTexture(property.PropertyId, value.TextureValue);
                    break;
                default:
                    throw new NotSupportedException("Unsupported shader property type.");
            }
        }

        private static RuntimeShaderPropertyValue ReadGlobal(RuntimeShaderPropertyDescriptor property)
        {
            switch (property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    return RuntimeShaderPropertyValue.Float(property.Type, Shader.GetGlobalFloat(property.PropertyId));
                case RuntimeShaderPropertyType.Integer:
                    return RuntimeShaderPropertyValue.Integer(Shader.GetGlobalInteger(property.PropertyId));
                case RuntimeShaderPropertyType.Color:
                    return RuntimeShaderPropertyValue.Color(Shader.GetGlobalColor(property.PropertyId));
                case RuntimeShaderPropertyType.Vector:
                    return RuntimeShaderPropertyValue.Vector(Shader.GetGlobalVector(property.PropertyId));
                case RuntimeShaderPropertyType.Texture:
                    return RuntimeShaderPropertyValue.Texture(Shader.GetGlobalTexture(property.PropertyId));
                default:
                    throw new NotSupportedException("Unsupported shader property type.");
            }
        }

        private static void WriteGlobal(RuntimeShaderPropertyDescriptor property, RuntimeShaderPropertyValue value)
        {
            switch (property.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    Shader.SetGlobalFloat(property.PropertyId, value.FloatValue);
                    break;
                case RuntimeShaderPropertyType.Integer:
                    Shader.SetGlobalInteger(property.PropertyId, value.IntegerValue);
                    break;
                case RuntimeShaderPropertyType.Color:
                    Shader.SetGlobalColor(property.PropertyId, value.ColorValue);
                    break;
                case RuntimeShaderPropertyType.Vector:
                    Shader.SetGlobalVector(property.PropertyId, value.VectorValue);
                    break;
                case RuntimeShaderPropertyType.Texture:
                    Shader.SetGlobalTexture(property.PropertyId, value.TextureValue);
                    break;
                default:
                    throw new NotSupportedException("Unsupported shader property type.");
            }
        }

        private static Texture ResolveRendererTexture(Renderer renderer,
            RuntimeShaderPropertyDescriptor property)
        {
            if (property.Type != RuntimeShaderPropertyType.Texture ||
                renderer is not SpriteRenderer spriteRenderer || spriteRenderer.sprite == null ||
                (!property.IsMainTexture && !string.Equals(property.Name, "_MainTex", StringComparison.Ordinal)))
                return null;

            return spriteRenderer.sprite.texture;
        }

        private static string Format(RuntimeShaderPropertyValue value,
            RuntimeShaderPropertyDescriptor property = null)
        {
            switch (value.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    return value.FloatValue.ToString("R", CultureInfo.InvariantCulture);
                case RuntimeShaderPropertyType.Integer:
                    return value.IntegerValue.ToString(CultureInfo.InvariantCulture);
                case RuntimeShaderPropertyType.Color:
                    return Join(value.ColorValue.r, value.ColorValue.g, value.ColorValue.b, value.ColorValue.a);
                case RuntimeShaderPropertyType.Vector:
                    return Join(value.VectorValue.x, value.VectorValue.y, value.VectorValue.z, value.VectorValue.w);
                case RuntimeShaderPropertyType.Texture:
                    Texture texture = value.TextureValue;
                    if (texture != null)
                        return $"{texture.name} ({texture.width}x{texture.height}, {texture.GetType().Name})";
                    return string.IsNullOrWhiteSpace(property?.DefaultTextureName)
                        ? "None (Texture)"
                        : $"Default: {property.DefaultTextureName}";
                default:
                    return "<unsupported>";
            }
        }

        private static string Join(float x, float y, float z, float w) => string.Join(", ", x.ToString("R", CultureInfo.InvariantCulture), y.ToString("R", CultureInfo.InvariantCulture), z.ToString("R", CultureInfo.InvariantCulture), w.ToString("R", CultureInfo.InvariantCulture));

        private static bool TryParse(string text, RuntimeShaderPropertyType type, out RuntimeShaderPropertyValue value, out string error)
        {
            switch (type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float scalar) && IsFinite(scalar))
                    {
                        value = RuntimeShaderPropertyValue.Float(type, scalar);
                        error = null;
                        return true;
                    }

                    value = default;
                    error = "Enter a finite number using invariant decimal notation.";
                    return false;
                case RuntimeShaderPropertyType.Integer:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integer))
                    {
                        value = RuntimeShaderPropertyValue.Integer(integer);
                        error = null;
                        return true;
                    }

                    value = default;
                    error = "Enter a whole number.";
                    return false;
                case RuntimeShaderPropertyType.Color:
                case RuntimeShaderPropertyType.Vector:
                    if (TryParseVector(text, out Vector4 vector))
                    {
                        value = type == RuntimeShaderPropertyType.Color ? RuntimeShaderPropertyValue.Color(new Color(vector.x, vector.y, vector.z, vector.w)) : RuntimeShaderPropertyValue.Vector(vector);
                        error = null;
                        return true;
                    }

                    value = default;
                    error = "Enter four finite values: X, Y, Z, W.";
                    return false;
                case RuntimeShaderPropertyType.Texture:
                    if (string.IsNullOrWhiteSpace(text) || string.Equals(text.Trim(), "null", StringComparison.OrdinalIgnoreCase) || string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                    {
                        value = RuntimeShaderPropertyValue.Texture(null);
                        error = null;
                        return true;
                    }

                    value = default;
                    error = "Texture assignment requires a runtime texture registry; enter 'null' to clear it.";
                    return false;
                default:
                    value = default;
                    error = "Unsupported shader property type.";
                    return false;
            }
        }

        private static bool TryParseVector(string text, out Vector4 vector)
        {
            vector = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string cleaned = text.Trim().Trim('(', ')', '[', ']');
            string[] parts = cleaned.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
                return false;

            var values = new float[4];
            for (int i = 0; i < values.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !IsFinite(values[i]))
                    return false;
            }

            vector = new Vector4(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static bool ValuesEqual(RuntimeShaderPropertyValue left, RuntimeShaderPropertyValue right)
        {
            if (left.Type != right.Type)
                return false;
            switch (left.Type)
            {
                case RuntimeShaderPropertyType.Float:
                case RuntimeShaderPropertyType.Range:
                    return left.FloatValue.Equals(right.FloatValue);
                case RuntimeShaderPropertyType.Integer:
                    return left.IntegerValue == right.IntegerValue;
                case RuntimeShaderPropertyType.Color:
                    return left.ColorValue.Equals(right.ColorValue);
                case RuntimeShaderPropertyType.Vector:
                    return left.VectorValue.Equals(right.VectorValue);
                case RuntimeShaderPropertyType.Texture:
                    return left.TextureValue == right.TextureValue;
                default:
                    return false;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private readonly struct SlotKey : IEquatable<SlotKey>
        {
            public SlotKey(int rendererInstanceId, int materialIndex)
            {
                RendererInstanceId = rendererInstanceId;
                MaterialIndex = materialIndex;
            }

            public int RendererInstanceId { get; }
            public int MaterialIndex { get; }
            public bool Equals(SlotKey other) => RendererInstanceId == other.RendererInstanceId && MaterialIndex == other.MaterialIndex;
            public override bool Equals(object obj) => obj is SlotKey other && Equals(other);
            public override int GetHashCode() => unchecked(RendererInstanceId * 397 ^ MaterialIndex);
        }

        private readonly struct PropertyKey : IEquatable<PropertyKey>
        {
            public PropertyKey(int rendererInstanceId, int materialIndex, int propertyId)
            {
                RendererInstanceId = rendererInstanceId;
                MaterialIndex = materialIndex;
                PropertyId = propertyId;
            }

            public int RendererInstanceId { get; }
            public int MaterialIndex { get; }
            public int PropertyId { get; }
            public bool Equals(PropertyKey other) => RendererInstanceId == other.RendererInstanceId && MaterialIndex == other.MaterialIndex && PropertyId == other.PropertyId;
            public override bool Equals(object obj) => obj is PropertyKey other && Equals(other);
            public override int GetHashCode() => unchecked((RendererInstanceId * 397 ^ MaterialIndex) * 397 ^ PropertyId);
        }

        private readonly struct MaterialPropertyKey : IEquatable<MaterialPropertyKey>
        {
            public MaterialPropertyKey(int materialInstanceId, int propertyId)
            {
                MaterialInstanceId = materialInstanceId;
                PropertyId = propertyId;
            }

            public int MaterialInstanceId { get; }
            public int PropertyId { get; }
            public bool Equals(MaterialPropertyKey other) => MaterialInstanceId == other.MaterialInstanceId && PropertyId == other.PropertyId;
            public override bool Equals(object obj) => obj is MaterialPropertyKey other && Equals(other);
            public override int GetHashCode() => unchecked(MaterialInstanceId * 397 ^ PropertyId);
        }

        private sealed class PropertyBlockOverrideRecord
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public RuntimeShaderPropertyDescriptor Property;
            public RuntimeShaderPropertyValue CurrentValue;
        }

        private sealed class PropertyBlockSlotRecord
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public MaterialPropertyBlock OriginalBlock;
            public bool OriginalWasEmpty;
        }

        private sealed class MaterialInstanceRecord
        {
            public Renderer Renderer;
            public int MaterialIndex;
            public Material OriginalMaterial;
            public Material InstanceMaterial;
        }

        private sealed class MaterialOverrideRecord
        {
            public Material Material;
            public RuntimeShaderPropertyDescriptor Property;
            public RuntimeShaderPropertyValue OriginalValue;
            public RuntimeShaderPropertyValue CurrentValue;
        }

        private sealed class GlobalOverrideRecord
        {
            public RuntimeShaderPropertyDescriptor Property;
            public RuntimeShaderPropertyValue OriginalValue;
            public RuntimeShaderPropertyValue CurrentValue;
        }
    }
}

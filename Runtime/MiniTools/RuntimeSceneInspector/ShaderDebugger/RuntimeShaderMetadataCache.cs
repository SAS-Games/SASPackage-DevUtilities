using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    public interface IRuntimeShaderMetadataCache
    {
        RuntimeShaderDescriptor GetOrCreate(Shader shader);
        void Clear();
    }

    public sealed class RuntimeShaderMetadataCache : IRuntimeShaderMetadataCache
    {
        private sealed class CacheEntry
        {
            public Shader Shader;
            public RuntimeShaderDescriptor Descriptor;
        }

        private readonly Dictionary<int, CacheEntry> _cache = new();

        public RuntimeShaderDescriptor GetOrCreate(Shader shader)
        {
            if (shader == null)
                return null;

            int instanceId = shader.GetInstanceID();
            if (_cache.TryGetValue(instanceId, out CacheEntry entry) && entry.Shader == shader)
                return entry.Descriptor;

            RuntimeShaderDescriptor descriptor = Build(shader);
            _cache[instanceId] = new CacheEntry { Shader = shader, Descriptor = descriptor };
            return descriptor;
        }

        public void Clear() => _cache.Clear();

        private static RuntimeShaderDescriptor Build(Shader shader)
        {
            int propertyCount = shader.GetPropertyCount();
            var properties = new List<RuntimeShaderPropertyDescriptor>(propertyCount);
            for (int index = 0; index < propertyCount; index++)
            {
                ShaderPropertyType unityType = shader.GetPropertyType(index);
                var property = new RuntimeShaderPropertyDescriptor
                {
                    Index = index,
                    PropertyId = shader.GetPropertyNameId(index),
                    Name = shader.GetPropertyName(index),
                    DisplayName = shader.GetPropertyDescription(index),
                    Type = MapType(unityType),
                    Flags = shader.GetPropertyFlags(index)
                };

                if (string.IsNullOrWhiteSpace(property.DisplayName))
                    property.DisplayName = property.Name;

                if (unityType == ShaderPropertyType.Range)
                {
                    Vector2 limits = shader.GetPropertyRangeLimits(index);
                    property.RangeMinimum = limits.x;
                    property.RangeMaximum = limits.y;
                }

                switch (unityType)
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                    case ShaderPropertyType.Int:
                        property.DefaultFloatValue = shader.GetPropertyDefaultFloatValue(index);
                        break;
                    case ShaderPropertyType.Color:
                    case ShaderPropertyType.Vector:
                        property.DefaultVectorValue = shader.GetPropertyDefaultVectorValue(index);
                        break;
                    case ShaderPropertyType.Texture:
                        property.DefaultTextureName = shader.GetPropertyTextureDefaultName(index);
                        break;
                }

                properties.Add(property);
            }

            return new RuntimeShaderDescriptor
            {
                ShaderInstanceId = shader.GetInstanceID(),
                ShaderName = shader.name,
                Properties = properties
            };
        }

        private static RuntimeShaderPropertyType MapType(ShaderPropertyType type)
        {
            switch (type)
            {
                case ShaderPropertyType.Float:
                    return RuntimeShaderPropertyType.Float;
                case ShaderPropertyType.Range:
                    return RuntimeShaderPropertyType.Range;
                case ShaderPropertyType.Int:
                    return RuntimeShaderPropertyType.Integer;
                case ShaderPropertyType.Color:
                    return RuntimeShaderPropertyType.Color;
                case ShaderPropertyType.Vector:
                    return RuntimeShaderPropertyType.Vector;
                case ShaderPropertyType.Texture:
                    return RuntimeShaderPropertyType.Texture;
                default:
                    return RuntimeShaderPropertyType.Unsupported;
            }
        }
    }
}

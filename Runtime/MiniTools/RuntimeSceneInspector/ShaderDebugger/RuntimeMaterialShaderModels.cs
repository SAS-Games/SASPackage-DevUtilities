using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    public enum RuntimeMaterialEditScope
    {
        RendererPropertyBlock,
        MaterialInstance,
        SharedMaterial,
        GlobalShaderProperty
    }

    public enum RuntimeShaderPropertyType
    {
        Float,
        Range,
        Integer,
        Color,
        Vector,
        Texture,
        Unsupported
    }

    [Serializable]
    public sealed class RuntimeShaderPropertyDescriptor
    {
        public int Index;
        public int PropertyId;
        public string Name;
        public string DisplayName;
        public RuntimeShaderPropertyType Type;
        public ShaderPropertyFlags Flags;
        public float RangeMinimum;
        public float RangeMaximum;
        public float DefaultFloatValue;
        public Vector4 DefaultVectorValue;
        public string DefaultTextureName;

        public bool IsHidden => (Flags & ShaderPropertyFlags.HideInInspector) != 0;
        public bool IsPerRendererData => (Flags & ShaderPropertyFlags.PerRendererData) != 0;
        public bool IsMainTexture => (Flags & ShaderPropertyFlags.MainTexture) != 0;
        public bool IsMainColor => (Flags & ShaderPropertyFlags.MainColor) != 0;
        public bool IsHdr => (Flags & ShaderPropertyFlags.HDR) != 0;
    }

    [Serializable]
    public sealed class RuntimeShaderDescriptor
    {
        public int ShaderInstanceId;
        public string ShaderName;
        public IReadOnlyList<RuntimeShaderPropertyDescriptor> Properties;
    }

    [Serializable]
    public sealed class RuntimeShaderPropertyView
    {
        public RuntimeShaderPropertyDescriptor Property;
        public string Value;
        public string ValueSource;
        public bool ReadOnly;
        public bool HasInspectorOverride;
    }

    [Serializable]
    public sealed class RuntimeMaterialSlotDescriptor
    {
        public int MaterialIndex;
        public string MaterialName;
        public int MaterialInstanceId;
        public string ShaderName;
        public int RenderQueue;
        public bool EnableInstancing;
        public bool MissingMaterial;
        public bool MissingShader;
        public bool IsInspectorMaterialInstance;
        public int TotalPropertyCount;
        public bool PropertyLimitReached;
        public IReadOnlyList<RuntimeShaderPropertyView> Properties;
    }

    [Serializable]
    public sealed class RuntimeRendererMaterialDescriptor
    {
        public RuntimeObjectId RendererId;
        public string RendererName;
        public string RendererType;
        public IReadOnlyList<RuntimeMaterialSlotDescriptor> MaterialSlots;
    }

    [Serializable]
    public sealed class RuntimeMaterialShaderSection
    {
        public string DisplayName = "Materials & Shaders";
        public IReadOnlyList<RuntimeRendererMaterialDescriptor> Renderers;
    }

    public readonly struct RuntimeShaderPropertyValue
    {
        private RuntimeShaderPropertyValue(RuntimeShaderPropertyType type, float floatValue, int integerValue, Color colorValue, Vector4 vectorValue, Texture textureValue)
        {
            Type = type;
            FloatValue = floatValue;
            IntegerValue = integerValue;
            ColorValue = colorValue;
            VectorValue = vectorValue;
            TextureValue = textureValue;
        }

        public RuntimeShaderPropertyType Type { get; }
        public float FloatValue { get; }
        public int IntegerValue { get; }
        public Color ColorValue { get; }
        public Vector4 VectorValue { get; }
        public Texture TextureValue { get; }

        public static RuntimeShaderPropertyValue Float(RuntimeShaderPropertyType type, float value) => new(type, value, default, default, default, null);

        public static RuntimeShaderPropertyValue Integer(int value) => new(RuntimeShaderPropertyType.Integer, default, value, default, default, null);

        public static RuntimeShaderPropertyValue Color(Color value) => new(RuntimeShaderPropertyType.Color, default, default, value, default, null);

        public static RuntimeShaderPropertyValue Vector(Vector4 value) => new(RuntimeShaderPropertyType.Vector, default, default, default, value, null);

        public static RuntimeShaderPropertyValue Texture(Texture value) => new(RuntimeShaderPropertyType.Texture, default, default, default, default, value);
    }
}

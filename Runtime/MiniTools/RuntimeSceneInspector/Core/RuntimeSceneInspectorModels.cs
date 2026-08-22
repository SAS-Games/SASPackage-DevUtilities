using System;
using System.Collections.Generic;
using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    internal sealed class RuntimeSceneInspectorProtectedAttribute : Attribute
    {
    }

    [Serializable]
    public readonly struct RuntimeObjectId : IEquatable<RuntimeObjectId>
    {
        public readonly long Value;
        public RuntimeObjectId(long value) => Value = value;
        public bool IsValid => Value > 0;
        public bool Equals(RuntimeObjectId other) => Value == other.Value;
        public static bool operator ==(RuntimeObjectId left, RuntimeObjectId right) => left.Equals(right);
        public static bool operator !=(RuntimeObjectId left, RuntimeObjectId right) => !left.Equals(right);
        public override bool Equals(object obj) => obj is RuntimeObjectId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    public enum RuntimeHierarchyKind
    {
        Scene,
        GameObject
    }

    [Serializable]
    public sealed class RuntimeHierarchyEntry
    {
        public RuntimeObjectId Id;
        public RuntimeObjectId ParentId;
        public RuntimeObjectId SceneId;
        public RuntimeHierarchyKind Kind;
        public string Name;
        public bool ActiveSelf;
        public bool ActiveInHierarchy;
        public string[] ComponentTypeNames;
    }

    [Serializable]
    public sealed class RuntimeHierarchySnapshot
    {
        public long Revision;
        public IReadOnlyList<RuntimeHierarchyEntry> Entries;
    }

    [Serializable]
    public enum RuntimeInspectorControlKind
    {
        Automatic = 0,
        Text = 1,
        Boolean = 2,
        Integer = 3,
        Float = 4,
        Enum = 5,
        EnumFlags = 6,
        Layer = 7,
        LayerMask = 8,
        SortingLayer = 9,
        Vector2 = 10,
        Vector2Int = 11,
        Vector3 = 12,
        Vector3Int = 13,
        Vector4 = 14,
        QuaternionEuler = 15,
        Color = 16,
        Color32 = 17,
        Rect = 18,
        Bounds = 19,
        ObjectReference = 20
    }

    [Flags]
    public enum RuntimeInspectorMemberCapabilities
    {
        None = 0,
        Edit = 1 << 0,
        Options = 1 << 1,
        Range = 1 << 2
    }

    [Serializable]
    public sealed class RuntimeInspectorOption
    {
        public string Label;
        public string Value;
        public long NumericValue;
        public bool HasNumericValue;
    }

    [Serializable]
    public sealed class RuntimeMemberDescriptor
    {
        public string Name;
        public string DisplayName;
        public string TypeName;
        public string Value;
        public bool ReadOnly;
        public string Error;
        public RuntimeInspectorControlKind ControlKind;
        public RuntimeInspectorMemberCapabilities Capabilities;
        public IReadOnlyList<RuntimeInspectorOption> Options;
        public bool HasRange;
        public float RangeMinimum;
        public float RangeMaximum;
    }

    [Serializable]
    public sealed class RuntimeComponentDescriptor
    {
        public RuntimeObjectId Id;
        public string TypeName;
        public bool HasEnabledState;
        public bool Enabled;
        public bool EnabledReadOnly;
        public bool Missing;
        public string StatusMessage;
        public IReadOnlyList<RuntimeMemberDescriptor> Members;
    }

    [Serializable]
    public sealed class RuntimeObjectDetails
    {
        public RuntimeObjectId Id;
        public string Name;
        public bool Active;
        public bool ActiveReadOnly;
        public string Tag;
        public int Layer;
        public bool LayerReadOnly;
        public IReadOnlyList<RuntimeComponentDescriptor> Components;
        public RuntimeMaterialShaderSection MaterialsAndShaders;
    }

    public sealed class RuntimeCommandResult
    {
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public static RuntimeCommandResult Ok(string message = "") => new() { Success = true, Message = message };
        public static RuntimeCommandResult Fail(string message) => new() { Message = message };
    }

    public abstract class RuntimeSceneInspectorCommand
    {
    }

    public sealed class SetGameObjectActiveCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId ObjectId;
        public bool Active;
    }

    public sealed class SetGameObjectLayerCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId ObjectId;
        public int Layer;
    }

    public sealed class SetComponentEnabledCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId ComponentId;
        public bool Enabled;
    }

    public sealed class SetMemberValueCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId ComponentId;
        public string MemberName;
        public string Value;
    }

    public sealed class SetRuntimeShaderPropertyCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId RendererId;
        public int MaterialIndex;
        public int PropertyId;
        public RuntimeMaterialEditScope Scope;
        public string Value;
    }

    public sealed class RestoreRuntimeShaderPropertyCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId RendererId;
        public int MaterialIndex;
        public int PropertyId;
        public RuntimeMaterialEditScope Scope;
    }

    public sealed class RestoreRuntimeMaterialCommand : RuntimeSceneInspectorCommand
    {
        public RuntimeObjectId RendererId;
        public int MaterialIndex;
        public RuntimeMaterialEditScope Scope;
    }

    public interface IRuntimeSceneInspector
    {
        RuntimeHierarchySnapshot GetHierarchySnapshot();
        RuntimeObjectDetails InspectObject(RuntimeObjectId objectId);
        RuntimeCommandResult Execute(RuntimeSceneInspectorCommand command);
        void RefreshHierarchy();
    }

    /// <summary>
    /// Optional local-runtime capability used by the scene-view object picker. Remote inspector
    /// implementations do not need to expose Unity object references.
    /// </summary>
    public interface IRuntimeSceneObjectResolver
    {
        bool TryGetObjectId(GameObject target, out RuntimeObjectId objectId);
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RuntimeInspectableAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RuntimeReadOnlyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RuntimeHiddenAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RuntimeRangeAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }

        public RuntimeRangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>
    /// Creates portable editor metadata on the Player, where the inspected types and project
    /// layer settings are authoritative. Remote editors should not need to load those types.
    /// </summary>
    internal static class RuntimeInspectorControlMetadata
    {
        private static readonly Dictionary<OptionCacheKey, IReadOnlyList<RuntimeInspectorOption>>
            OptionCache = new();

        internal static void Populate(RuntimeMemberDescriptor descriptor, Type valueType,
            string displayName, string memberId,
            RuntimeInspectorControlKind requestedKind = RuntimeInspectorControlKind.Automatic,
            RuntimeRangeAttribute range = null)
        {
            if (descriptor == null)
                return;

            RuntimeInspectorControlKind kind = requestedKind == RuntimeInspectorControlKind.Automatic
                ? ResolveKind(valueType, displayName, memberId)
                : requestedKind;
            descriptor.ControlKind = kind;
            descriptor.Options = BuildOptions(kind, valueType);
            descriptor.HasRange = range != null && range.Max >= range.Min;
            descriptor.RangeMinimum = range?.Min ?? 0f;
            descriptor.RangeMaximum = range?.Max ?? 0f;

            RuntimeInspectorMemberCapabilities capabilities = descriptor.ReadOnly
                ? RuntimeInspectorMemberCapabilities.None
                : RuntimeInspectorMemberCapabilities.Edit;
            if (descriptor.Options != null && descriptor.Options.Count > 0)
                capabilities |= RuntimeInspectorMemberCapabilities.Options;
            if (descriptor.HasRange)
                capabilities |= RuntimeInspectorMemberCapabilities.Range;
            descriptor.Capabilities = capabilities;
        }

        private static RuntimeInspectorControlKind ResolveKind(Type type, string displayName,
            string memberId)
        {
            if (type == null)
                return RuntimeInspectorControlKind.Text;
            if (type == typeof(bool))
                return RuntimeInspectorControlKind.Boolean;
            if (type.IsEnum)
                return type.IsDefined(typeof(FlagsAttribute), false)
                    ? RuntimeInspectorControlKind.EnumFlags
                    : RuntimeInspectorControlKind.Enum;
            if (type == typeof(LayerMask))
                return RuntimeInspectorControlKind.LayerMask;
            if ((type == typeof(int) || type == typeof(string)) &&
                IsSortingLayerIdentifier(displayName, memberId))
                return RuntimeInspectorControlKind.SortingLayer;
            if (type == typeof(int) && IsLayerIdentifier(displayName, memberId))
                return RuntimeInspectorControlKind.Layer;
            if (type == typeof(byte) || type == typeof(short) || type == typeof(int) ||
                type == typeof(long))
                return RuntimeInspectorControlKind.Integer;
            if (type == typeof(float) || type == typeof(double))
                return RuntimeInspectorControlKind.Float;
            if (type == typeof(Vector2))
                return RuntimeInspectorControlKind.Vector2;
            if (type == typeof(Vector2Int))
                return RuntimeInspectorControlKind.Vector2Int;
            if (type == typeof(Vector3))
                return RuntimeInspectorControlKind.Vector3;
            if (type == typeof(Vector3Int))
                return RuntimeInspectorControlKind.Vector3Int;
            if (type == typeof(Vector4))
                return RuntimeInspectorControlKind.Vector4;
            if (type == typeof(Quaternion))
                return RuntimeInspectorControlKind.QuaternionEuler;
            if (type == typeof(Color))
                return RuntimeInspectorControlKind.Color;
            if (type == typeof(Color32))
                return RuntimeInspectorControlKind.Color32;
            if (type == typeof(Rect))
                return RuntimeInspectorControlKind.Rect;
            if (type == typeof(Bounds))
                return RuntimeInspectorControlKind.Bounds;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return RuntimeInspectorControlKind.ObjectReference;
            return RuntimeInspectorControlKind.Text;
        }

        private static IReadOnlyList<RuntimeInspectorOption> BuildOptions(
            RuntimeInspectorControlKind kind, Type valueType)
        {
            if (kind != RuntimeInspectorControlKind.Enum &&
                kind != RuntimeInspectorControlKind.EnumFlags &&
                kind != RuntimeInspectorControlKind.Layer &&
                kind != RuntimeInspectorControlKind.LayerMask &&
                kind != RuntimeInspectorControlKind.SortingLayer)
                return Array.Empty<RuntimeInspectorOption>();

            var key = new OptionCacheKey(kind, valueType);
            if (OptionCache.TryGetValue(key, out IReadOnlyList<RuntimeInspectorOption> cached))
                return cached;

            IReadOnlyList<RuntimeInspectorOption> options;
            switch (kind)
            {
                case RuntimeInspectorControlKind.Enum:
                case RuntimeInspectorControlKind.EnumFlags:
                    options = BuildEnumOptions(valueType);
                    break;
                case RuntimeInspectorControlKind.Layer:
                case RuntimeInspectorControlKind.LayerMask:
                    options = BuildLayerOptions();
                    break;
                case RuntimeInspectorControlKind.SortingLayer:
                    options = BuildSortingLayerOptions(valueType);
                    break;
                default:
                    options = Array.Empty<RuntimeInspectorOption>();
                    break;
            }
            OptionCache[key] = options;
            return options;
        }

        private static IReadOnlyList<RuntimeInspectorOption> BuildEnumOptions(Type enumType)
        {
            if (enumType == null || !enumType.IsEnum)
                return Array.Empty<RuntimeInspectorOption>();

            string[] names = Enum.GetNames(enumType);
            Array values = Enum.GetValues(enumType);
            var options = new RuntimeInspectorOption[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                options[i] = new RuntimeInspectorOption
                {
                    Label = names[i],
                    Value = names[i],
                    NumericValue = ToInt64(values.GetValue(i)),
                    HasNumericValue = true
                };
            }

            return options;
        }

        private static IReadOnlyList<RuntimeInspectorOption> BuildLayerOptions()
        {
            var options = new RuntimeInspectorOption[32];
            for (int i = 0; i < options.Length; i++)
            {
                string name = LayerMask.LayerToName(i);
                options[i] = new RuntimeInspectorOption
                {
                    Label = string.IsNullOrEmpty(name) ? "Layer " + i : name,
                    Value = i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    NumericValue = i,
                    HasNumericValue = true
                };
            }

            return options;
        }

        private static IReadOnlyList<RuntimeInspectorOption> BuildSortingLayerOptions(Type valueType)
        {
            SortingLayer[] layers = SortingLayer.layers;
            var options = new RuntimeInspectorOption[layers.Length];
            bool useName = valueType == typeof(string);
            for (int i = 0; i < layers.Length; i++)
            {
                options[i] = new RuntimeInspectorOption
                {
                    Label = layers[i].name,
                    Value = useName
                        ? layers[i].name
                        : layers[i].id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    NumericValue = layers[i].id,
                    HasNumericValue = true
                };
            }

            return options;
        }

        private static long ToInt64(object enumValue)
        {
            try
            {
                return Convert.ToInt64(enumValue, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return unchecked((long)Convert.ToUInt64(enumValue,
                    System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        internal static bool IsLayerIdentifier(string displayName, string memberId)
        {
            string target = (displayName ?? memberId ?? string.Empty).Replace(" ", string.Empty);
            return target.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   target.IndexOf("Sorting", StringComparison.OrdinalIgnoreCase) < 0 &&
                   target.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) < 0;
        }

        internal static bool IsSortingLayerIdentifier(string displayName, string memberId)
        {
            string target = (displayName ?? memberId ?? string.Empty).Replace(" ", string.Empty);
            return target.IndexOf("SortingLayer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   target.IndexOf("Sorting", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   target.IndexOf("Layer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private readonly struct OptionCacheKey : IEquatable<OptionCacheKey>
        {
            private readonly RuntimeInspectorControlKind _kind;
            private readonly Type _valueType;

            internal OptionCacheKey(RuntimeInspectorControlKind kind, Type valueType)
            {
                _kind = kind;
                _valueType = valueType;
            }

            public bool Equals(OptionCacheKey other) =>
                _kind == other._kind && _valueType == other._valueType;

            public override bool Equals(object obj) =>
                obj is OptionCacheKey other && Equals(other);

            public override int GetHashCode() =>
                ((int)_kind * 397) ^ (_valueType?.GetHashCode() ?? 0);
        }
    }
}

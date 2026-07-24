using System;
using System.Collections.Generic;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    internal sealed class RuntimeDebuggerProtectedAttribute : Attribute
    {
    }

    [Serializable]
    public readonly struct RuntimeObjectId : IEquatable<RuntimeObjectId>
    {
        public readonly long Value;
        public RuntimeObjectId(long value) => Value = value;
        public bool IsValid => Value > 0;
        public bool Equals(RuntimeObjectId other) => Value == other.Value;
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
    public sealed class RuntimeMemberDescriptor
    {
        public string Name;
        public string DisplayName;
        public string TypeName;
        public string Value;
        public bool ReadOnly;
        public string Error;
    }

    [Serializable]
    public sealed class RuntimeComponentDescriptor
    {
        public RuntimeObjectId Id;
        public string TypeName;
        public bool HasEnabledState;
        public bool Enabled;
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
        public string Tag;
        public int Layer;
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

    public abstract class RuntimeDebuggerCommand
    {
    }

    public sealed class SetGameObjectActiveCommand : RuntimeDebuggerCommand
    {
        public RuntimeObjectId ObjectId;
        public bool Active;
    }

    public sealed class SetComponentEnabledCommand : RuntimeDebuggerCommand
    {
        public RuntimeObjectId ComponentId;
        public bool Enabled;
    }

    public sealed class SetMemberValueCommand : RuntimeDebuggerCommand
    {
        public RuntimeObjectId ComponentId;
        public string MemberName;
        public string Value;
    }

    public sealed class SetRuntimeShaderPropertyCommand : RuntimeDebuggerCommand
    {
        public RuntimeObjectId RendererId;
        public int MaterialIndex;
        public int PropertyId;
        public RuntimeMaterialEditScope Scope;
        public string Value;
    }

    public sealed class RestoreRuntimeShaderPropertyCommand : RuntimeDebuggerCommand
    {
        public RuntimeObjectId RendererId;
        public int MaterialIndex;
        public int PropertyId;
        public RuntimeMaterialEditScope Scope;
    }

    public sealed class RestoreRuntimeMaterialCommand : RuntimeDebuggerCommand
    {
        public RuntimeObjectId RendererId;
        public int MaterialIndex;
        public RuntimeMaterialEditScope Scope;
    }

    public interface IRuntimeDebugger
    {
        RuntimeHierarchySnapshot GetHierarchySnapshot();
        RuntimeObjectDetails InspectObject(RuntimeObjectId objectId);
        RuntimeCommandResult Execute(RuntimeDebuggerCommand command);
        void RefreshHierarchy();
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
}

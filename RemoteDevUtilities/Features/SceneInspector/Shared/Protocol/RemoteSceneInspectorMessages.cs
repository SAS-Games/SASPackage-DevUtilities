using System;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector
{
    public static class RemoteSceneInspectorMessageTypes
    {
        public const string SceneInspectorHierarchyRequest = "scene-inspector.hierarchy.request";
        public const string SceneInspectorHierarchyResponse = "scene-inspector.hierarchy.response";
        public const string SceneInspectorInspectRequest = "scene-inspector.inspect.request";
        public const string SceneInspectorInspectResponse = "scene-inspector.inspect.response";
        public const string SceneInspectorCommandRequest = "scene-inspector.command.request";
        public const string SceneInspectorCommandResponse = "scene-inspector.command.response";
        public const string SceneInspectorCaptureRequest = "scene-inspector.capture.request";
        public const string SceneInspectorCaptureResponse = "scene-inspector.capture.response";
        public const string SceneInspectorPickRequest = "scene-inspector.pick.request";
        public const string SceneInspectorPickResponse = "scene-inspector.pick.response";
    }

    [Serializable]
    public sealed class RemoteSceneInspectorHierarchyRequest
    {
        public bool ForceRefresh;
    }

    [Serializable]
    public sealed class RemoteSceneInspectorHierarchyResponse
    {
        public long Revision;
        public RemoteHierarchyEntry[] Entries = Array.Empty<RemoteHierarchyEntry>();
    }

    [Serializable]
    public sealed class RemoteHierarchyEntry
    {
        public long Id;
        public long ParentId;
        public long SceneId;
        public int Kind;
        public string Name;
        public bool ActiveSelf;
        public bool ActiveInHierarchy;
        public string[] ComponentTypeNames = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RemoteSceneInspectorInspectRequest
    {
        public long ObjectId;
    }

    [Serializable]
    public sealed class RemoteSceneInspectorInspectResponse
    {
        public bool Found;
        public string Error;
        public RemoteObjectDetails Details;
    }

    [Serializable]
    public sealed class RemoteObjectDetails
    {
        public long Id;
        public string Name;
        public bool Active;
        public string Tag;
        public int Layer;
        public RemoteComponentDescriptor[] Components = Array.Empty<RemoteComponentDescriptor>();
        public RemoteMaterialShaderSection MaterialsAndShaders;
    }

    [Serializable]
    public sealed class RemoteComponentDescriptor
    {
        public long Id;
        public string TypeName;
        public bool HasEnabledState;
        public bool Enabled;
        public bool Missing;
        public string StatusMessage;
        public RemoteMemberDescriptor[] Members = Array.Empty<RemoteMemberDescriptor>();
    }

    [Serializable]
    public sealed class RemoteMemberDescriptor
    {
        public string Name;
        public string DisplayName;
        public string TypeName;
        public string Value;
        public bool ReadOnly;
        public string Error;
    }

    [Serializable]
    public sealed class RemoteMaterialShaderSection
    {
        public string DisplayName;
        public RemoteRendererMaterialDescriptor[] Renderers = Array.Empty<RemoteRendererMaterialDescriptor>();
    }

    [Serializable]
    public sealed class RemoteRendererMaterialDescriptor
    {
        public long RendererId;
        public string RendererName;
        public string RendererType;
        public RemoteMaterialSlotDescriptor[] MaterialSlots = Array.Empty<RemoteMaterialSlotDescriptor>();
    }

    [Serializable]
    public sealed class RemoteMaterialSlotDescriptor
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
        public RemoteShaderPropertyView[] Properties = Array.Empty<RemoteShaderPropertyView>();
    }

    [Serializable]
    public sealed class RemoteShaderPropertyView
    {
        public int Index;
        public int PropertyId;
        public string Name;
        public string DisplayName;
        public int Type;
        public int Flags;
        public float RangeMinimum;
        public float RangeMaximum;
        public float DefaultFloatValue;
        public Vector4 DefaultVectorValue;
        public string DefaultTextureName;
        public string Value;
        public string ValueSource;
        public bool ReadOnly;
        public bool HasInspectorOverride;
    }

    public enum RemoteSceneInspectorCommandKind
    {
        SetGameObjectActive,
        SetComponentEnabled,
        SetMemberValue,
        SetShaderProperty,
        RestoreShaderProperty,
        RestoreMaterial
    }

    [Serializable]
    public sealed class RemoteSceneInspectorCommandRequest
    {
        public RemoteSceneInspectorCommandKind Kind;
        public long ObjectId;
        public long ComponentId;
        public long RendererId;
        public bool BooleanValue;
        public string MemberName;
        public string Value;
        public int MaterialIndex;
        public int PropertyId;
        public int MaterialScope;
    }

    [Serializable]
    public sealed class RemoteSceneInspectorCommandResponse
    {
        public bool Success;
        public string Message;
    }
}

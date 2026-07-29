using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    internal sealed class RuntimeColliderComponentDrawer : RuntimeComponentDrawer<Collider>
    {
        public RuntimeColliderComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.isTrigger", "Is Trigger", collider => collider.isTrigger,
                (collider, value) => collider.isTrigger = value);
            Add("@unity.contactOffset", "Contact Offset", collider => collider.contactOffset,
                (collider, value) => collider.contactOffset = value,
                validator: (_, value) => RequirePositive(value, "Contact offset"));
#if UNITY_2022_2_OR_NEWER
            Add("@unity.includeLayers", "Include Layers", collider => collider.includeLayers,
                (collider, value) => collider.includeLayers = value);
            Add("@unity.excludeLayers", "Exclude Layers", collider => collider.excludeLayers,
                (collider, value) => collider.excludeLayers = value);
            Add("@unity.layerOverridePriority", "Layer Override Priority", collider => collider.layerOverridePriority,
                (collider, value) => collider.layerOverridePriority = value);
#endif
            AddReadOnly("@unity.bounds", "Bounds", collider => collider.bounds);
            AddReadOnly("@unity.sharedMaterial", "Shared Material", collider => collider.sharedMaterial);
            AddReadOnly("@unity.attachedRigidbody", "Attached Rigidbody", collider => collider.attachedRigidbody);

            Add("@unity.center", "Center", collider => ((BoxCollider)collider).center,
                (collider, value) => ((BoxCollider)collider).center = value,
                applies: collider => collider is BoxCollider);
            Add("@unity.size", "Size", collider => ((BoxCollider)collider).size,
                (collider, value) => ((BoxCollider)collider).size = value,
                collider => collider is BoxCollider,
                (_, value) => RequireNonNegative(value, "Size"));

            Add("@unity.center", "Center", collider => ((SphereCollider)collider).center,
                (collider, value) => ((SphereCollider)collider).center = value,
                applies: collider => collider is SphereCollider);
            Add("@unity.radius", "Radius", collider => ((SphereCollider)collider).radius,
                (collider, value) => ((SphereCollider)collider).radius = value,
                collider => collider is SphereCollider,
                (_, value) => RequireNonNegative(value, "Radius"));

            Add("@unity.center", "Center", collider => ((CapsuleCollider)collider).center,
                (collider, value) => ((CapsuleCollider)collider).center = value,
                applies: collider => collider is CapsuleCollider);
            Add("@unity.radius", "Radius", collider => ((CapsuleCollider)collider).radius,
                (collider, value) => ((CapsuleCollider)collider).radius = value,
                collider => collider is CapsuleCollider,
                (_, value) => RequireNonNegative(value, "Radius"));
            Add("@unity.height", "Height", collider => ((CapsuleCollider)collider).height,
                (collider, value) => ((CapsuleCollider)collider).height = value,
                collider => collider is CapsuleCollider,
                (_, value) => RequireNonNegative(value, "Height"));
            Add("@unity.direction", "Direction", collider => ((CapsuleCollider)collider).direction,
                (collider, value) => ((CapsuleCollider)collider).direction = value,
                collider => collider is CapsuleCollider,
                (_, value) => value >= 0 && value <= 2 ? null : "Direction must be 0, 1, or 2.");

            Add("@unity.convex", "Convex", collider => ((MeshCollider)collider).convex,
                (collider, value) => ((MeshCollider)collider).convex = value,
                applies: collider => collider is MeshCollider);
            AddReadOnly("@unity.sharedMesh", "Shared Mesh", collider => ((MeshCollider)collider).sharedMesh,
                collider => collider is MeshCollider);
        }
    }
}


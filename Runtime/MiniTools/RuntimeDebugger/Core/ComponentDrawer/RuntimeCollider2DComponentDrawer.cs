using UnityEngine;

namespace SAS.Utilities.RuntimeDebugger.Core
{
    internal sealed class RuntimeCollider2DComponentDrawer : RuntimeComponentDrawer<Collider2D>
    {
        public RuntimeCollider2DComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.isTrigger", "Is Trigger", collider => collider.isTrigger,
                (collider, value) => collider.isTrigger = value);
            Add("@unity.offset", "Offset", collider => collider.offset,
                (collider, value) => collider.offset = value);
            Add("@unity.density", "Density", collider => collider.density,
                (collider, value) => collider.density = value,
                validator: (_, value) => RequireNonNegative(value, "Density"));
            Add("@unity.usedByEffector", "Used By Effector", collider => collider.usedByEffector,
                (collider, value) => collider.usedByEffector = value);
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
            AddReadOnly("@unity.shapeCount", "Shape Count", collider => collider.shapeCount);
            AddReadOnly("@unity.friction", "Friction", collider => collider.friction);
            AddReadOnly("@unity.bounciness", "Bounciness", collider => collider.bounciness);

            Add("@unity.size", "Size", collider => ((BoxCollider2D)collider).size,
                (collider, value) => ((BoxCollider2D)collider).size = value,
                collider => collider is BoxCollider2D,
                (_, value) => RequireNonNegative(value, "Size"));
            Add("@unity.edgeRadius", "Edge Radius", collider => ((BoxCollider2D)collider).edgeRadius,
                (collider, value) => ((BoxCollider2D)collider).edgeRadius = value,
                collider => collider is BoxCollider2D,
                (_, value) => RequireNonNegative(value, "Edge radius"));

            Add("@unity.radius", "Radius", collider => ((CircleCollider2D)collider).radius,
                (collider, value) => ((CircleCollider2D)collider).radius = value,
                collider => collider is CircleCollider2D,
                (_, value) => RequireNonNegative(value, "Radius"));

            Add("@unity.size", "Size", collider => ((CapsuleCollider2D)collider).size,
                (collider, value) => ((CapsuleCollider2D)collider).size = value,
                collider => collider is CapsuleCollider2D,
                (_, value) => RequireNonNegative(value, "Size"));
            Add("@unity.direction", "Direction", collider => ((CapsuleCollider2D)collider).direction,
                (collider, value) => ((CapsuleCollider2D)collider).direction = value,
                applies: collider => collider is CapsuleCollider2D);

            Add("@unity.edgeRadius", "Edge Radius", collider => ((EdgeCollider2D)collider).edgeRadius,
                (collider, value) => ((EdgeCollider2D)collider).edgeRadius = value,
                collider => collider is EdgeCollider2D,
                (_, value) => RequireNonNegative(value, "Edge radius"));
            AddReadOnly("@unity.pointCount", "Point Count", collider => ((EdgeCollider2D)collider).pointCount,
                collider => collider is EdgeCollider2D);
            AddReadOnly("@unity.pathCount", "Path Count", collider => ((PolygonCollider2D)collider).pathCount,
                collider => collider is PolygonCollider2D);

            Add("@unity.geometryType", "Geometry Type", collider => ((CompositeCollider2D)collider).geometryType,
                (collider, value) => ((CompositeCollider2D)collider).geometryType = value,
                applies: collider => collider is CompositeCollider2D);
            Add("@unity.generationType", "Generation Type",
                collider => ((CompositeCollider2D)collider).generationType,
                (collider, value) => ((CompositeCollider2D)collider).generationType = value,
                applies: collider => collider is CompositeCollider2D);
            AddReadOnly("@unity.pathCount", "Path Count", collider => ((CompositeCollider2D)collider).pathCount,
                collider => collider is CompositeCollider2D);
            AddReadOnly("@unity.pointCount", "Point Count", collider => ((CompositeCollider2D)collider).pointCount,
                collider => collider is CompositeCollider2D);
        }
    }
}


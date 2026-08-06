using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeRendererComponentDrawer : RuntimeComponentDrawer<Renderer>
    {
        public RuntimeRendererComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.forceRenderingOff", "Force Rendering Off", renderer => renderer.forceRenderingOff, (renderer, value) => renderer.forceRenderingOff = value);
            Add("@unity.shadowCastingMode", "Shadow Casting Mode", renderer => renderer.shadowCastingMode, (renderer, value) => renderer.shadowCastingMode = value);
            Add("@unity.receiveShadows", "Receive Shadows", renderer => renderer.receiveShadows, (renderer, value) => renderer.receiveShadows = value);
            Add("@unity.lightProbeUsage", "Light Probe Usage", renderer => renderer.lightProbeUsage, (renderer, value) => renderer.lightProbeUsage = value);
            Add("@unity.reflectionProbeUsage", "Reflection Probe Usage", renderer => renderer.reflectionProbeUsage, (renderer, value) => renderer.reflectionProbeUsage = value);
            Add("@unity.motionVectorGenerationMode", "Motion Vector Mode", renderer => renderer.motionVectorGenerationMode, (renderer, value) => renderer.motionVectorGenerationMode = value);
            Add("@unity.allowOcclusionWhenDynamic", "Allow Dynamic Occlusion", renderer => renderer.allowOcclusionWhenDynamic, (renderer, value) => renderer.allowOcclusionWhenDynamic = value);
            Add("@unity.sortingLayerId", "Sorting Layer ID", renderer => renderer.sortingLayerID, (renderer, value) => renderer.sortingLayerID = value);
            Add("@unity.sortingOrder", "Sorting Order", renderer => renderer.sortingOrder, (renderer, value) => renderer.sortingOrder = value);
            AddReadOnly("@unity.bounds", "World Bounds", renderer => renderer.bounds);
            AddReadOnly("@unity.localBounds", "Local Bounds", renderer => renderer.localBounds);
            AddReadOnly("@unity.sharedMaterial", "Shared Material", renderer => renderer.sharedMaterial);
        }
    }
}

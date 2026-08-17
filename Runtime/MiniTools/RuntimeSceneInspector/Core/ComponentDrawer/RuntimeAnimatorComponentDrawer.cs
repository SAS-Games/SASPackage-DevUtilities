using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeAnimatorComponentDrawer : RuntimeComponentDrawer<Animator>
    {
        public RuntimeAnimatorComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            // Changing this property reinitializes the Animator, so inspection is intentionally read-only.
            AddReadOnly("@unity.applyRootMotion", "Apply Root Motion", animator => animator.applyRootMotion);
            Add("@unity.updateMode", "Update Mode", animator => animator.updateMode, (animator, value) => animator.updateMode = value);
            Add("@unity.cullingMode", "Culling Mode", animator => animator.cullingMode, (animator, value) => animator.cullingMode = value);
            Add("@unity.speed", "Speed", animator => animator.speed, (animator, value) => animator.speed = value);
            Add("@unity.fireEvents", "Fire Events", animator => animator.fireEvents, (animator, value) => animator.fireEvents = value);
            Add("@unity.keepAnimatorStateOnDisable", "Keep State On Disable", animator => animator.keepAnimatorStateOnDisable, (animator, value) => animator.keepAnimatorStateOnDisable = value);
            Add("@unity.stabilizeFeet", "Stabilize Feet", animator => animator.stabilizeFeet, (animator, value) => animator.stabilizeFeet = value, animator => animator.isHuman);
            AddReadOnly("@unity.runtimeAnimatorController", "Runtime Animator Controller", animator => animator.runtimeAnimatorController);
            AddReadOnly("@unity.avatar", "Avatar", animator => animator.avatar);
            AddReadOnly("@unity.isInitialized", "Is Initialized", animator => animator.isInitialized);
            AddReadOnly("@unity.isHuman", "Is Human", animator => animator.isHuman);
            AddReadOnly("@unity.hasRootMotion", "Has Root Motion", animator => animator.hasRootMotion);
            AddReadOnly("@unity.layerCount", "Layer Count", animator => animator.layerCount);
            AddReadOnly("@unity.parameterCount", "Parameter Count", animator => animator.parameterCount);
            AddReadOnly("@unity.velocity", "Velocity", animator => animator.velocity);
            AddReadOnly("@unity.angularVelocity", "Angular Velocity", animator => animator.angularVelocity);
            AddReadOnly("@unity.deltaPosition", "Delta Position", animator => animator.deltaPosition);
            AddReadOnly("@unity.deltaRotation", "Delta Rotation", animator => animator.deltaRotation);
        }
    }
}

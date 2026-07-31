using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeRigidbodyComponentDrawer : RuntimeComponentDrawer<Rigidbody>
    {
        public RuntimeRigidbodyComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.mass", "Mass", body => body.mass, (body, value) => body.mass = value, validator: (_, value) => RequirePositive(value, "Mass"));
#if UNITY_6000_0_OR_NEWER
            Add("@unity.linearDamping", "Linear Damping", body => body.linearDamping,
                (body, value) => body.linearDamping = value,
                validator: (_, value) => RequireNonNegative(value, "Linear damping"));
            Add("@unity.angularDamping", "Angular Damping", body => body.angularDamping,
                (body, value) => body.angularDamping = value,
                validator: (_, value) => RequireNonNegative(value, "Angular damping"));
            Add("@unity.linearVelocity", "Linear Velocity", body => body.linearVelocity,
                (body, value) => body.linearVelocity = value);
#else
            Add("@unity.linearDamping", "Linear Damping", body => body.drag, (body, value) => body.drag = value, validator: (_, value) => RequireNonNegative(value, "Linear damping"));
            Add("@unity.angularDamping", "Angular Damping", body => body.angularDrag, (body, value) => body.angularDrag = value, validator: (_, value) => RequireNonNegative(value, "Angular damping"));
            Add("@unity.linearVelocity", "Linear Velocity", body => body.velocity, (body, value) => body.velocity = value);
#endif
            Add("@unity.angularVelocity", "Angular Velocity", body => body.angularVelocity, (body, value) => body.angularVelocity = value);
            Add("@unity.useGravity", "Use Gravity", body => body.useGravity, (body, value) => body.useGravity = value);
            Add("@unity.isKinematic", "Is Kinematic", body => body.isKinematic, (body, value) => body.isKinematic = value);
            Add("@unity.detectCollisions", "Detect Collisions", body => body.detectCollisions, (body, value) => body.detectCollisions = value);
            Add("@unity.interpolation", "Interpolation", body => body.interpolation, (body, value) => body.interpolation = value);
            Add("@unity.collisionDetectionMode", "Collision Detection", body => body.collisionDetectionMode, (body, value) => body.collisionDetectionMode = value);
            Add("@unity.constraints", "Constraints", body => body.constraints, (body, value) => body.constraints = value);
            Add("@unity.maxAngularVelocity", "Max Angular Velocity", body => body.maxAngularVelocity, (body, value) => body.maxAngularVelocity = value, validator: (_, value) => RequireNonNegative(value, "Maximum angular velocity"));
            AddReadOnly("@unity.worldCenterOfMass", "World Center Of Mass", body => body.worldCenterOfMass);
        }
    }
}

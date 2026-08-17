using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeRigidbody2DComponentDrawer : RuntimeComponentDrawer<Rigidbody2D>
    {
        public RuntimeRigidbody2DComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.bodyType", "Body Type", body => body.bodyType, (body, value) => body.bodyType = value);
            Add("@unity.simulated", "Simulated", body => body.simulated, (body, value) => body.simulated = value);
            Add("@unity.useAutoMass", "Use Auto Mass", body => body.useAutoMass, (body, value) => body.useAutoMass = value);
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
            Add("@unity.gravityScale", "Gravity Scale", body => body.gravityScale, (body, value) => body.gravityScale = value);
            Add("@unity.interpolation", "Interpolation", body => body.interpolation, (body, value) => body.interpolation = value);
            Add("@unity.collisionDetectionMode", "Collision Detection", body => body.collisionDetectionMode, (body, value) => body.collisionDetectionMode = value);
            Add("@unity.constraints", "Constraints", body => body.constraints, (body, value) => body.constraints = value);
            Add("@unity.useFullKinematicContacts", "Use Full Kinematic Contacts", body => body.useFullKinematicContacts, (body, value) => body.useFullKinematicContacts = value);
            AddReadOnly("@unity.attachedColliderCount", "Attached Collider Count", body => body.attachedColliderCount);
            AddReadOnly("@unity.worldCenterOfMass", "World Center Of Mass", body => body.worldCenterOfMass);
        }
    }
}

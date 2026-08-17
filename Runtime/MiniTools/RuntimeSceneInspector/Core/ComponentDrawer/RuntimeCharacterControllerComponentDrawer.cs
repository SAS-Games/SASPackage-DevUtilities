using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeCharacterControllerComponentDrawer : RuntimeComponentDrawer<CharacterController>
    {
        public RuntimeCharacterControllerComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.center", "Center", controller => controller.center, (controller, value) => controller.center = value);
            Add("@unity.radius", "Radius", controller => controller.radius, (controller, value) => controller.radius = value, validator: (_, value) => RequirePositive(value, "Radius"));
            Add("@unity.height", "Height", controller => controller.height, (controller, value) => controller.height = value, validator: (_, value) => RequirePositive(value, "Height"));
            Add("@unity.slopeLimit", "Slope Limit", controller => controller.slopeLimit, (controller, value) => controller.slopeLimit = value, validator: (_, value) => value >= 0f && value <= 90f ? null : "Slope limit must be between 0 and 90 degrees.");
            Add("@unity.stepOffset", "Step Offset", controller => controller.stepOffset, (controller, value) => controller.stepOffset = value, validator: (_, value) => RequireNonNegative(value, "Step offset"));
            Add("@unity.skinWidth", "Skin Width", controller => controller.skinWidth, (controller, value) => controller.skinWidth = value, validator: (_, value) => RequirePositive(value, "Skin width"));
            Add("@unity.minMoveDistance", "Minimum Move Distance", controller => controller.minMoveDistance, (controller, value) => controller.minMoveDistance = value, validator: (_, value) => RequireNonNegative(value, "Minimum move distance"));
            Add("@unity.detectCollisions", "Detect Collisions", controller => controller.detectCollisions, (controller, value) => controller.detectCollisions = value);
            Add("@unity.enableOverlapRecovery", "Enable Overlap Recovery", controller => controller.enableOverlapRecovery, (controller, value) => controller.enableOverlapRecovery = value);
            AddReadOnly("@unity.isGrounded", "Is Grounded", controller => controller.isGrounded);
            AddReadOnly("@unity.velocity", "Velocity", controller => controller.velocity);
            AddReadOnly("@unity.collisionFlags", "Collision Flags", controller => controller.collisionFlags);
        }
    }
}

using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeTransformComponentDrawer : RuntimeComponentDrawer<Transform>
    {
        public RuntimeTransformComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.localPosition", "Local Position", transform => transform.localPosition, SetLocalPosition);
            Add("@unity.localEulerAngles", "Local Rotation", transform => transform.localEulerAngles, (transform, value) => transform.localEulerAngles = value);
            Add("@unity.localScale", "Local Scale", transform => transform.localScale, (transform, value) => transform.localScale = value);
            Add("@unity.position", "World Position", transform => transform.position, SetWorldPosition);
            Add("@unity.eulerAngles", "World Rotation", transform => transform.eulerAngles, (transform, value) => transform.eulerAngles = value);
            Add("@unity.hasChanged", "Has Changed", transform => transform.hasChanged, (transform, value) => transform.hasChanged = value);
            AddReadOnly("@unity.lossyScale", "World Scale", transform => transform.lossyScale);
            AddReadOnly("@unity.forward", "Forward", transform => transform.forward);
            AddReadOnly("@unity.up", "Up", transform => transform.up);
            AddReadOnly("@unity.right", "Right", transform => transform.right);
            AddReadOnly("@unity.childCount", "Child Count", transform => transform.childCount);
        }

        private static void SetLocalPosition(Transform transform, Vector3 value)
        {
            SetPosition(transform, value, true);
        }

        private static void SetWorldPosition(Transform transform, Vector3 value)
        {
            SetPosition(transform, value, false);
        }

        private static void SetPosition(Transform transform, Vector3 value, bool local)
        {
            CharacterController controller = transform.GetComponent<CharacterController>();
            bool restoreController = controller != null && controller.enabled;
            if (restoreController)
                controller.enabled = false;

            try
            {
                if (local)
                    transform.localPosition = value;
                else
                    transform.position = value;
                SynchronizePhysicsTransforms();
            }
            finally
            {
                if (restoreController && controller != null)
                    controller.enabled = true;
            }
        }

        private static void SynchronizePhysicsTransforms()
        {
            // Runtime projects commonly disable automatic Transform-to-physics synchronization.
            // Without this explicit sync, the next physics step can restore the previous body pose.
            Physics.SyncTransforms();
            Physics2D.SyncTransforms();
        }
    }
}

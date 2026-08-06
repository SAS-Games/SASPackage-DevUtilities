using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeTransformComponentDrawer : RuntimeComponentDrawer<Transform>
    {
        public RuntimeTransformComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.localPosition", "Local Position", transform => transform.localPosition, (transform, value) => transform.localPosition = value);
            Add("@unity.localEulerAngles", "Local Rotation", transform => transform.localEulerAngles, (transform, value) => transform.localEulerAngles = value);
            Add("@unity.localScale", "Local Scale", transform => transform.localScale, (transform, value) => transform.localScale = value);
            Add("@unity.position", "World Position", transform => transform.position, (transform, value) => transform.position = value);
            Add("@unity.eulerAngles", "World Rotation", transform => transform.eulerAngles, (transform, value) => transform.eulerAngles = value);
            Add("@unity.hasChanged", "Has Changed", transform => transform.hasChanged, (transform, value) => transform.hasChanged = value);
            AddReadOnly("@unity.lossyScale", "World Scale", transform => transform.lossyScale);
            AddReadOnly("@unity.forward", "Forward", transform => transform.forward);
            AddReadOnly("@unity.up", "Up", transform => transform.up);
            AddReadOnly("@unity.right", "Right", transform => transform.right);
            AddReadOnly("@unity.childCount", "Child Count", transform => transform.childCount);
        }
    }
}

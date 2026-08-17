using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeRectTransformComponentDrawer : RuntimeComponentDrawer<RectTransform>
    {
        public RuntimeRectTransformComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.anchorMin", "Anchor Min", transform => transform.anchorMin, (transform, value) => transform.anchorMin = value);
            Add("@unity.anchorMax", "Anchor Max", transform => transform.anchorMax, (transform, value) => transform.anchorMax = value);
            Add("@unity.anchoredPosition", "Anchored Position", transform => transform.anchoredPosition, (transform, value) => transform.anchoredPosition = value);
            Add("@unity.anchoredPosition3D", "Anchored Position 3D", transform => transform.anchoredPosition3D, (transform, value) => transform.anchoredPosition3D = value);
            Add("@unity.sizeDelta", "Size Delta", transform => transform.sizeDelta, (transform, value) => transform.sizeDelta = value);
            Add("@unity.pivot", "Pivot", transform => transform.pivot, (transform, value) => transform.pivot = value);
            Add("@unity.offsetMin", "Offset Min", transform => transform.offsetMin, (transform, value) => transform.offsetMin = value);
            Add("@unity.offsetMax", "Offset Max", transform => transform.offsetMax, (transform, value) => transform.offsetMax = value);
            AddReadOnly("@unity.rect", "Rect", transform => transform.rect);
        }
    }
}

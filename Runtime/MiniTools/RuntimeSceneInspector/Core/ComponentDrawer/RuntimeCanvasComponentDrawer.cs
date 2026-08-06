using UnityEngine;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeCanvasComponentDrawer : RuntimeComponentDrawer<Canvas>
    {
        public RuntimeCanvasComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.renderMode", "Render Mode", canvas => canvas.renderMode, (canvas, value) => canvas.renderMode = value);
            Add("@unity.pixelPerfect", "Pixel Perfect", canvas => canvas.pixelPerfect, (canvas, value) => canvas.pixelPerfect = value);
            Add("@unity.overrideSorting", "Override Sorting", canvas => canvas.overrideSorting, (canvas, value) => canvas.overrideSorting = value);
            Add("@unity.sortingLayerId", "Sorting Layer ID", canvas => canvas.sortingLayerID, (canvas, value) => canvas.sortingLayerID = value);
            Add("@unity.sortingOrder", "Sorting Order", canvas => canvas.sortingOrder, (canvas, value) => canvas.sortingOrder = value);
            Add("@unity.targetDisplay", "Target Display", canvas => canvas.targetDisplay, (canvas, value) => canvas.targetDisplay = value, validator: (_, value) => value >= 0 ? null : "Target display cannot be negative.");
            Add("@unity.planeDistance", "Plane Distance", canvas => canvas.planeDistance, (canvas, value) => canvas.planeDistance = value, canvas => canvas.renderMode == RenderMode.ScreenSpaceCamera, (_, value) => RequirePositive(value, "Plane distance"));
            Add("@unity.scaleFactor", "Scale Factor", canvas => canvas.scaleFactor, (canvas, value) => canvas.scaleFactor = value, validator: (_, value) => RequirePositive(value, "Scale factor"));
            Add("@unity.referencePixelsPerUnit", "Reference Pixels Per Unit", canvas => canvas.referencePixelsPerUnit, (canvas, value) => canvas.referencePixelsPerUnit = value, validator: (_, value) => RequirePositive(value, "Reference pixels per unit"));
            AddReadOnly("@unity.worldCamera", "World Camera", canvas => canvas.worldCamera);
            AddReadOnly("@unity.rootCanvas", "Root Canvas", canvas => canvas.rootCanvas);
        }
    }

    internal sealed class RuntimeCanvasGroupComponentDrawer : RuntimeComponentDrawer<CanvasGroup>
    {
        public RuntimeCanvasGroupComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.alpha", "Alpha", group => group.alpha, (group, value) => group.alpha = value, validator: (_, value) => value >= 0f && value <= 1f ? null : "Alpha must be between 0 and 1.");
            Add("@unity.interactable", "Interactable", group => group.interactable, (group, value) => group.interactable = value);
            Add("@unity.blocksRaycasts", "Blocks Raycasts", group => group.blocksRaycasts, (group, value) => group.blocksRaycasts = value);
            Add("@unity.ignoreParentGroups", "Ignore Parent Groups", group => group.ignoreParentGroups, (group, value) => group.ignoreParentGroups = value);
        }
    }
}

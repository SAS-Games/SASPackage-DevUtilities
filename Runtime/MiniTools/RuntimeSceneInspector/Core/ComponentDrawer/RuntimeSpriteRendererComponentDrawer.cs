using UnityEngine;

namespace HP.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeSpriteRendererComponentDrawer : RuntimeComponentDrawer<SpriteRenderer>
    {
        public RuntimeSpriteRendererComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            AddReadOnly("@unity.sprite", "Sprite", renderer => renderer.sprite);
            Add("@unity.color", "Color", renderer => renderer.color, (renderer, value) => renderer.color = value);
            Add("@unity.flipX", "Flip X", renderer => renderer.flipX, (renderer, value) => renderer.flipX = value);
            Add("@unity.flipY", "Flip Y", renderer => renderer.flipY, (renderer, value) => renderer.flipY = value);
            Add("@unity.drawMode", "Draw Mode", renderer => renderer.drawMode, (renderer, value) => renderer.drawMode = value);
            Add("@unity.size", "Size", renderer => renderer.size, (renderer, value) => renderer.size = value,
                renderer => renderer.drawMode != SpriteDrawMode.Simple,
                (_, value) => RequireNonNegative(value, "Size"));
            Add("@unity.tileMode", "Tile Mode", renderer => renderer.tileMode, (renderer, value) => renderer.tileMode = value,
                renderer => renderer.drawMode == SpriteDrawMode.Tiled);
            Add("@unity.adaptiveModeThreshold", "Adaptive Mode Threshold", renderer => renderer.adaptiveModeThreshold,
                (renderer, value) => renderer.adaptiveModeThreshold = value,
                renderer => renderer.drawMode == SpriteDrawMode.Tiled && renderer.tileMode == SpriteTileMode.Adaptive,
                (_, value) => RequireNonNegative(value, "Adaptive mode threshold"));
            Add("@unity.maskInteraction", "Mask Interaction", renderer => renderer.maskInteraction, (renderer, value) => renderer.maskInteraction = value);
            Add("@unity.spriteSortPoint", "Sprite Sort Point", renderer => renderer.spriteSortPoint, (renderer, value) => renderer.spriteSortPoint = value);
        }
    }
}

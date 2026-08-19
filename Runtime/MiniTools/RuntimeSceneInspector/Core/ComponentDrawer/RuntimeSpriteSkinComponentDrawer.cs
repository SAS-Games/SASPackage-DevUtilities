#if UNITY_2D_ANIMATION
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace SAS.Utilities.RuntimeSceneInspector.Core
{
    internal sealed class RuntimeSpriteSkinComponentDrawer : RuntimeComponentDrawer<SpriteSkin>
    {
        public RuntimeSpriteSkinComponentDrawer(RuntimeValueDrawerRegistry valueDrawers) : base(valueDrawers)
        {
            Add("@unity.autoRebind", "Auto Rebind", spriteSkin => spriteSkin.autoRebind,
                (spriteSkin, value) => spriteSkin.autoRebind = value);
            Add("@unity.alwaysUpdate", "Always Update", spriteSkin => spriteSkin.alwaysUpdate,
                (spriteSkin, value) => spriteSkin.alwaysUpdate = value);
            AddReadOnly("@unity.rootBone", "Root Bone", spriteSkin => spriteSkin.rootBone);
            AddReadOnly("@unity.boneTransforms", "Bone Transforms", spriteSkin => spriteSkin.boneTransforms);
        }
    }
}
#endif

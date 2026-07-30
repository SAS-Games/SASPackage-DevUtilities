using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Providers
{
    [UnityEngine.Scripting.Preserve]
    internal sealed class RuntimeAnimatorMiniToolProvider :
        MiniToolFieldDataProvider
    {
        public override RemoteMiniToolField[] CaptureFields()
        {
            Animator[] animators = Object.FindObjectsByType<Animator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int active = 0;
            int initialized = 0;
            int alwaysAnimate = 0;
            int culled = 0;

            foreach (Animator animator in animators)
            {
                if (animator == null)
                    continue;
                if (animator.enabled && animator.gameObject.activeInHierarchy)
                    active++;
                if (animator.isInitialized)
                    initialized++;
                if (animator.cullingMode == AnimatorCullingMode.AlwaysAnimate)
                    alwaysAnimate++;
                else if (animator.cullingMode == AnimatorCullingMode.CullCompletely)
                    culled++;
            }

            return new[]
            {
                CreateField(
                    "total",
                    "Total Animators",
                    animators.Length.ToString()),
                CreateField(
                    "active",
                    "Active Animators",
                    active.ToString()),
                CreateField(
                    "initialized",
                    "Initialized",
                    initialized.ToString()),
                CreateField(
                    "alwaysAnimate",
                    "Always Animate",
                    alwaysAnimate.ToString()),
                CreateField(
                    "cullCompletely",
                    "Cull Completely",
                    culled.ToString())
            };
        }
    }
}

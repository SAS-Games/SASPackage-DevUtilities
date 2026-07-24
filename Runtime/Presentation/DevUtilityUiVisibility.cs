using UnityEngine;

namespace SAS.Utilities.Presentation
{
    /// <summary>
    /// Resolves a prefab-owned visibility controller without coupling commands to its implementation.
    /// </summary>
    public static class DevUtilityUiVisibility
    {
        public static void SetVisible(GameObject root, bool visible)
        {
            if (root == null)
                return;

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not IDevUtilityUiVisibility visibility)
                    continue;

                visibility.SetVisible(visible);
                return;
            }

            root.SetActive(visible);
        }
    }
}

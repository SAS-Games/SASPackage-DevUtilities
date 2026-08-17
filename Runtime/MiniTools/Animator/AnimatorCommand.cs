using HP.DevUtilities;
using HP.Utilities.Presentation;
using UnityEngine;

namespace HP.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Animator Command", menuName = DeveloperConsole.CommandBasePath + "Animator Command")]
    public class AnimatorCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_AnimatorStatsPrefab;
        private GameObject _statsInstance;
        private DevUtilityPresentation _presentation;

        public override string HelpText => "";

        private bool ShowStats(string[] args)
        {
            if (args == null || args.Length == 0 || !BoolUtil.TryParse(args[0], out bool isVisible))
                return false;

            EnsureStatsInstance();
            if (_presentation == null)
                return false;

            _presentation.SetRequestedVisible(isVisible);
            return true;
        }

        private bool SetCulling(string[] args)
        {
            if (args == null || args.Length < 1)
                return false;

            AnimatorCullingMode mode;
            switch (args[0].ToLowerInvariant())
            {
                case "always":
                    mode = AnimatorCullingMode.AlwaysAnimate;
                    break;
                case "cullupdate":
                case "update":
                    mode = AnimatorCullingMode.CullUpdateTransforms;
                    break;
                case "cull":
                    mode = AnimatorCullingMode.CullCompletely;
                    break;
                default:
                    Debug.LogError("Unknown mode. Use: always | cullupdate | cull");
                    return false;
            }

            Animator[] animatorsInScene = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Animator animator in animatorsInScene)
            {
                if (animator != null)
                    animator.cullingMode = mode;
            }

            Debug.Log($"AnimatorCull: Set mode = {mode} on {animatorsInScene.Length} animators");
            return true;
        }

        private bool Refresh(string[] args)
        {
            EnsureStatsInstance();
            AnimatorStatsSnapshotProvider provider = _statsInstance.GetComponent<AnimatorStatsSnapshotProvider>();
            if (provider == null)
                return false;

            provider.Refresh();
            if (_presentation == null)
                return false;

            _presentation.SetRequestedVisible(true);
            Debug.Log("Animator Stats UI Refreshed.");
            return true;
        }

        private void EnsureStatsInstance()
        {
            if (_statsInstance != null)
                return;

            _statsInstance = Instantiate(m_AnimatorStatsPrefab);
            _statsInstance.name = "AnimatorStatsUI";
            _presentation = _statsInstance.GetComponent<DevUtilityPresentation>();
        }
    }
}

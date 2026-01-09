using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Animator Command", menuName = "SAS/DeveloperConsole/Commands/Animator Command")]
    public class AnimatorCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_AnimatorStatsPrefab;
        private GameObject _statsInstance;
        public override string HelpText => "";

        private bool ShowStats(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_statsInstance == null)
                    {
                        _statsInstance = Object.Instantiate(m_AnimatorStatsPrefab);
                        _statsInstance.name = "AnimatorStatsUI";
                    }

                    _statsInstance.SetActive(isVisible);
                    return true;
                }
            }

            return false;
        }

        private bool SetCulling(string[] args)
        {
            if (args == null || args.Length < 1)
                return false;

            AnimatorCullingMode mode;

            switch (args[0].ToLower())
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
            foreach (var animator in animatorsInScene)
                animator.cullingMode = mode;

            Debug.Log($"AnimatorCull: Set mode = {mode} on {animatorsInScene.Length} animators");
            return true;
        }

        private bool Refresh(string[] args)
        {

            if (_statsInstance == null)
            {
                _statsInstance = Instantiate(m_AnimatorStatsPrefab);
                _statsInstance.name = "ParticleStatsUI";
            }

            _statsInstance.SetActive(false);
            _statsInstance.SetActive(true);
            Debug.Log("Particle Stats UI Refreshed.");

            return true;
        }
    }
}

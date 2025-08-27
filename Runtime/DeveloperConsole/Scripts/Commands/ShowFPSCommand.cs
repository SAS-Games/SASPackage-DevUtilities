using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command", menuName = "SAS/DeveloperConsole/Commands/Show FPS Command")]
    public class ShowFPSCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_FpsPrefab;
        private GameObject _fps;
        public override string HelpText => "Usage: FPS [true/false]. Show/Hide FPS UI.";
        protected override void CommandMethodRegistry()
        {
            Register("Show", ShowFPS);
            Register("SetTargetFrameRate", SetTargetFrameRate);
        }

        private bool ShowFPS(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (bool.TryParse(args[0], out var isVisible))
                {
                    if (_fps == null)
                        _fps = Instantiate(m_FpsPrefab);

                    _fps.SetActive(isVisible);
                    return true;
                }
            }

            return false;
        }


        private bool SetTargetFrameRate(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int val))
                return false;

            // Disable VSync so targetFrameRate takes effect
            QualitySettings.vSyncCount = 0;

            // Apply frame rate (-1 = platform default)
            Application.targetFrameRate = Mathf.Max(val, -1);

            return true;
        }
    }
}
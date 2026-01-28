using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command", menuName =  DeveloperConsole.CommandBasePath + "Show FPS Command")]
    public class ShowFPSCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_FpsPrefab;
        private GameObject _fps;
        public override string HelpText => "Usage: FPS <On|Off>. \nShow/Hide FPS UI.";

        protected bool ShowFPS(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_fps == null)
                    {
                        _fps = Instantiate(m_FpsPrefab);
                        _fps.name = "FPSCanvas";
                    }

                    if (args.Length > 1)
                    {
                        int paddingX = 0;
                        int paddingY = 0;

                        // Get padding if passed
                        if (args.Length > 2 && !int.TryParse(args[2], out paddingX))
                            return false;

                        if (args.Length > 3 && !int.TryParse(args[3], out paddingY))
                            return false;

                        RectTransform fpsRect = _fps.transform.GetChild(0).transform as RectTransform;
                        fpsRect.AlignToScreen(args[1], paddingX, paddingY);
                    }

                    _fps.SetActive(isVisible);
                    return true;
                }
            }

            return false;
        }


        protected virtual bool SetTargetFrameRate(string[] args)
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
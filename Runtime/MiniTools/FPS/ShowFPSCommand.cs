using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command", menuName = DeveloperConsole.CommandBasePath + "Show FPS Command")]
    public class ShowFPSCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_FpsPrefab;
        private GameObject _fps;

        public override string HelpText => "Stats commands:\n" +
                                           "  Stats.FPS <On|Off> [anchor] [horizontal-padding] [vertical-padding]\n" +
                                           "  Stats.SetTargetFrameRate <-1|fps>";

        protected bool ShowFPS(string[] args)
        {
#if !ENABLE_DEBUG
            return false;
#else
            if (args == null || args.Length < 1 || args.Length > 4)
                return false;

            if (!BoolUtil.TryParse(args[0], out bool isVisible))
                return false;

            bool hasAlignment = args.Length > 1;
            Vector2 anchor = default;
            int paddingX = 0;
            int paddingY = 0;

            // Validate every argument before creating, moving, or toggling the overlay.
            if (hasAlignment && !AnchorPreset.TryGetAnchorValues(args[1], out anchor))
                return false;

            if (args.Length > 2 && !int.TryParse(args[2], out paddingX))
                return false;

            if (args.Length > 3 && !int.TryParse(args[3], out paddingY))
                return false;

            // Hiding an overlay that does not exist is already a successful no-op.
            if (!isVisible && _fps == null)
                return true;

            if (_fps == null)
            {
                if (m_FpsPrefab == null)
                    return false;

                if (hasAlignment && !TryGetDisplayRect(m_FpsPrefab, out _))
                    return false;

                _fps = Instantiate(m_FpsPrefab);
                _fps.name = "FPSCanvas";
            }

            if (hasAlignment)
            {
                if (!TryGetDisplayRect(_fps, out RectTransform fpsRect))
                    return false;

                fpsRect.AlignToScreen(anchor, new Vector2Int(paddingX, paddingY));
            }

            Presentation.DevUtilityUiVisibility.SetVisible(_fps, isVisible);
            return true;
#endif
        }

        protected virtual bool SetTargetFrameRate(string[] args)
        {
#if !ENABLE_DEBUG
            return false;
#else
            if (!TryParseTargetFrameRate(args, out int targetFrameRate))
                return false;

            // Application.targetFrameRate is authoritative only when VSync is disabled.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            return true;
#endif
        }

        protected static bool TryParseTargetFrameRate(string[] args, out int targetFrameRate)
        {
            targetFrameRate = 0;
            return args != null &&
                   args.Length == 1 &&
                   int.TryParse(args[0], out targetFrameRate) &&
                   (targetFrameRate == -1 || targetFrameRate > 0);
        }

        private static bool TryGetDisplayRect(GameObject root, out RectTransform displayRect)
        {
            displayRect = null;
            if (root == null || root.transform.childCount == 0)
                return false;

            displayRect = root.transform.GetChild(0) as RectTransform;
            return displayRect != null;
        }
    }
}

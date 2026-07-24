using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = DeveloperConsole.CommandBasePath + "Display Console Command")]
    public class DisplayConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText;
        public override string HelpText => m_HelpText;

        private bool _hasSnapshot;
        private int _originalWidth;
        private int _originalHeight;
        private FullScreenMode _originalMode;

        private bool SetResolution(string[] args)
        {
            if (args == null || args.Length != 3) return false;

            if (!int.TryParse(args[0], out int width) || width <= 0) return false;
            if (!int.TryParse(args[1], out int height) || height <= 0) return false;
            if (!BoolUtil.TryParse(args[2], out bool fullscreen)) return false;

            CaptureSnapshot();
            Screen.SetResolution(width, height, fullscreen);

            Debug.Log($"Resolution set to {width}x{height}, Fullscreen: {fullscreen}");
            return true;
        }

        private bool SetFullScreen(string[] args)
        {
            if (args == null || args.Length != 1) return false;

            if (BoolUtil.TryParse(args[0], out bool fullscreen))
            {
                CaptureSnapshot();
                Screen.fullScreen = fullscreen;
                return true;
            }

            return false;
        }

        private bool SetWindowMode(string[] args)
        {
            if (args == null || args.Length != 1) return false;

            FullScreenMode mode;
            switch (args[0].ToLowerInvariant())
            {
                case "windowed":
                    mode = FullScreenMode.Windowed;
                    break;
                case "borderless":
                case "fullscreenwindow":
                    mode = FullScreenMode.FullScreenWindow;
                    break;
                case "exclusive":
                case "exclusivefullscreen":
                    mode = FullScreenMode.ExclusiveFullScreen;
                    break;
                case "maximized":
                case "maximizedwindow":
                    mode = FullScreenMode.MaximizedWindow;
                    break;
                default:
                    return false;
            }

            CaptureSnapshot();
            Screen.fullScreenMode = mode;
            Debug.Log($"Window mode set to {Screen.fullScreenMode}");
            return true;
        }

        private bool Status(string[] args)
        {
            if (args == null || args.Length != 0)
                return false;
            Resolution resolution = Screen.currentResolution;
            Debug.Log($"[Display] Window={Screen.width}x{Screen.height}, Mode={Screen.fullScreenMode}, Fullscreen={Screen.fullScreen}, Display={resolution.width}x{resolution.height} @ {resolution.refreshRateRatio.value:F2} Hz");
            return true;
        }

        private bool Restore(string[] args)
        {
            if (args == null || args.Length != 0)
                return false;
            if (_hasSnapshot)
            {
                Screen.SetResolution(_originalWidth, _originalHeight, _originalMode);
                _hasSnapshot = false;
            }
            return true;
        }

        private void CaptureSnapshot()
        {
            if (_hasSnapshot)
                return;
            _originalWidth = Screen.width;
            _originalHeight = Screen.height;
            _originalMode = Screen.fullScreenMode;
            _hasSnapshot = true;
        }
    }
}

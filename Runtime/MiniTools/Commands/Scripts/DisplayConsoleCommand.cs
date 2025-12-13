using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(menuName = DeveloperConsole.CommandBasePath + "Display Console Command")]
    public class DisplayConsoleCommand : CompositeConsoleCommand
    {
        [SerializeField] private string m_HelpText;
        public override string HelpText => m_HelpText;

        protected override void CommandMethodRegistry()
        {
            Register("SetResolution", SetResolution);
            Register("SetFullScreen", SetFullScreen);
            Register("SetWindowMode", SetWindowMode);
        }

        private bool SetResolution(string[] args)
        {
            if (args.Length < 2) return false;

            if (!int.TryParse(args[0], out int width)) return false;
            if (!int.TryParse(args[1], out int height)) return false;

            BoolUtil.TryParse(args[2], out bool fullscreen);
            Screen.SetResolution(width, height, fullscreen);

            Debug.Log($"Resolution set to {width}x{height}, Fullscreen: {fullscreen}");
            return true;
        }

        private bool SetFullScreen(string[] args)
        {
            if (args.Length < 1) return false;

            if (BoolUtil.TryParse(args[0], out bool fullscreen))
            {
                Screen.fullScreen = fullscreen;
                return true;
            }

            return false;
        }

        private bool SetWindowMode(string[] args)
        {
            if (args.Length < 1) return false;

            switch (args[0].ToLower())
            {
                case "windowed":
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    break;
                case "borderless":
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
                case "exclusive":
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    break;
                case "maximized":
                    Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
                    break;
                default:
                    return false;
            }

            Debug.Log($"Window mode set to {Screen.fullScreenMode}");
            return true;
        }
    }
}
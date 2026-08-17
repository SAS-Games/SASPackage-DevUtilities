using HP.Utilities.Presentation;
using UnityEngine;

namespace HP.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Game Info Command", menuName = DeveloperConsole.CommandBasePath + "Game Info Command")]
    public class GameInfoCommand : ConsoleCommand
    {
        [SerializeField] private string m_HelpText;
        [SerializeField] private GameObject m_InfoPrefab;

        private GameObject _infoObj;
        private DevUtilityPresentation _presentation;
        public override string HelpText => m_HelpText;

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_infoObj == null)
                    {
                        _infoObj = Instantiate(m_InfoPrefab);
                        _presentation = _infoObj.GetComponent<DevUtilityPresentation>();
                    }

                    if (_presentation == null)
                        return false;

                    _presentation.SetRequestedVisible(isVisible);
                    return true;
                }
            }

            return false;
        }
    }
}

using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Game Info Command", menuName = "HP/DeveloperConsole/Commands/Game Info Command")]
    public class GameInfoCommand : ConsoleCommand
    {
        [SerializeField] private string m_HelpText;
        [SerializeField] private GameObject m_InfoPrefab;

        private GameObject _infoObj;
        public override string HelpText => m_HelpText;

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_infoObj == null)
                        _infoObj = Instantiate(m_InfoPrefab);

                    _infoObj.SetActive(isVisible);
                    return true;
                }
            }

            return false;
        }
    }
}
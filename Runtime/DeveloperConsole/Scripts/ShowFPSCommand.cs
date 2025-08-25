using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command", menuName = "SAS/DeveloperConsole/Commands/Show FPS Command")]
    public class ShowFPSCommand : ConsoleCommand
    {
        [SerializeField] private GameObject m_FpsPrefab;
        private GameObject _fps;
        public override string HelpText => "Usage: FPS [true/false]. Show/Hide FPS UI.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
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
    }
}
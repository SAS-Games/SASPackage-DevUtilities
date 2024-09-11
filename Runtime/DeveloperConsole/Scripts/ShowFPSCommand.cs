using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Show FPS Command", menuName = "SAS/Utilities/DeveloperConsole/Commands/Show FPS Command")]
    public class ShowFPSCommand : ConsoleCommand
    {
        [SerializeField] private GameObject m_FpsPrefab;

        private GameObject _fps;

        public override string HelpText => "Usage: FPS [true/false]. Show/Hide FPS UI.";

        public override bool Process(string[] args, DeveloperConsoleBehaviour developerConsole)
        {
            if (args.Length == 0)
                return false;
            if (bool.TryParse(args[0], out var show))
            {
                if (_fps == null)
                    _fps = Instantiate(m_FpsPrefab);
                _fps.SetActive(show);

                return true;
            }
            return false;
        }
    }
}

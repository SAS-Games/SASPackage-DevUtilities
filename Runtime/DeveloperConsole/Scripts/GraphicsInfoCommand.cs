using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "GraphicsInfoCommand", menuName = "SAS/DeveloperConsole/Commands/GraphicsInfo")]
    public class GraphicsInfoCommand : ConsoleCommand
    {
        [SerializeField] private GameObject m_GraphicsInfoPrefab;
        private GameObject _graphics;
        public override string HelpText => "Usage: GraphicsInfo [true/false] [verbose: true/false]\n" +
                                           "show current graphics, quality, and rendering settings.\n" +
                                           "Add 'verbose' for extended details.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args != null && args.Length > 0)
            {
                if (bool.TryParse(args[0], out var isVisible))
                {
                    if (_graphics == null)
                        _graphics = Instantiate(m_GraphicsInfoPrefab);
                    var verbose = false;
                    if (args.Length > 1 && args.Length > 1 && bool.TryParse(args[1], out verbose)) { }

                    var info = _graphics.GetComponent<GraphicsInfo>(); ;
                    info.Show(isVisible, verbose);
                    return true;
                }
            }

            return false;
        }
    }
}

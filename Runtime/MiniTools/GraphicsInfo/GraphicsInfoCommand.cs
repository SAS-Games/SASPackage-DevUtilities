using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "GraphicsInfoCommand", menuName =  DeveloperConsole.CommandBasePath + "GraphicsInfo")]
    public class GraphicsInfoCommand : ConsoleCommand
    {
        [SerializeField] private GameObject m_GraphicsInfoPrefab;
        private GameObject _graphics;

        public override string HelpText => "Usage: GraphicsInfo <On|Off> <verbose: Extended>\n" +
                                           "Show current graphics, quality, and rendering settings.\n" +
                                           "Use Extended or Verbose for extended details. With no arguments, the overlay is shown.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args == null || args.Length > 2)
                return false;

            bool isVisible = true;
            if (args.Length > 0 && !BoolUtil.TryParse(args[0], out isVisible))
                return false;

            bool verbose = false;
            if (args.Length == 2)
            {
                string detail = args[1].ToLowerInvariant();
                if (detail != "extended" && detail != "verbose")
                    return false;
                verbose = true;
            }

            if (!isVisible && _graphics == null)
                return true;
            if (_graphics == null)
            {
                if (m_GraphicsInfoPrefab == null)
                    return false;
                _graphics = Instantiate(m_GraphicsInfoPrefab);
                _graphics.name = "GraphicsInfo";
            }

            GraphicsInfo info = _graphics.GetComponent<GraphicsInfo>();
            if (info == null)
                return false;
            info.Show(isVisible, verbose);
            return true;
        }
    }
}

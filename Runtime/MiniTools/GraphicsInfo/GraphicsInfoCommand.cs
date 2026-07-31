using SAS.DevUtilities;
using SAS.Utilities.Presentation;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "GraphicsInfoCommand", menuName = DeveloperConsole.CommandBasePath + "GraphicsInfo")]
    public class GraphicsInfoCommand : ConsoleCommand
    {
        [SerializeField] private GameObject m_GraphicsInfoPrefab;
        private GameObject _graphics;
        private DevUtilityPresentation _presentation;

        public override string HelpText => "Usage: GraphicsInfo <On|Off> <verbose: Extended>\n" + "Show current graphics, quality, and rendering settings.\n" + "Add 'verbose' for extended details.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args == null || args.Length == 0 || !BoolUtil.TryParse(args[0], out bool isVisible))
                return false;

            if (_graphics == null)
            {
                _graphics = Instantiate(m_GraphicsInfoPrefab);
                _graphics.name = "GraphicsInfo";
                _presentation = _graphics.GetComponent<DevUtilityPresentation>();
            }

            if (_presentation == null)
                return false;

            bool verbose = args.Length > 1 && args[1].Equals("extended", System.StringComparison.OrdinalIgnoreCase);
            GraphicsInfoSnapshotProvider snapshotProvider = _graphics.GetComponent<GraphicsInfoSnapshotProvider>();
            if (snapshotProvider == null)
                return false;

            snapshotProvider.SetRequestedState(isVisible, verbose);
            _presentation.SetRequestedVisible(isVisible);
            return true;
        }
    }
}

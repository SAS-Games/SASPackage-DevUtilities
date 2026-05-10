using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Input Latency Profiler Command",
        menuName = DeveloperConsole.CommandBasePath + "Input Latency Profiler")]
    public class InputLatencyProfilerCommand : ConsoleCommand
    {
        [SerializeField] private GameObject m_InputLatencyProfilerPrefab;
        private GameObject _inputLatencyProfiler;
        public override string HelpText => "Usage: InputLatencyProfiler <On|Off>. \nShow/Hide InputLatencyProfiler UI.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_inputLatencyProfiler == null)
                    {
                        _inputLatencyProfiler = Instantiate(m_InputLatencyProfilerPrefab);
                        _inputLatencyProfiler.name = "InputLatencyProfiler";
                    }

                    _inputLatencyProfiler.SetActive(isVisible);
                    return true;
                }
            }

            return false;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Input Latency Profiler Command", menuName = DeveloperConsole.CommandBasePath + "Input Latency Profiler")]
    public class InputLatencyProfilerCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_InputLatencyProfilerPrefab;
        private GameObject _inputLatencyProfiler;
        public override string HelpText => "Usage: InputLatencyProfiler <On|Off>. \nShow/Hide InputLatencyProfiler UI.";
        

        private bool InputLatencyProfiler(string[] args)
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

        private bool InputUpdateMode(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            string mode = args[0].ToLowerInvariant();

            switch (mode)
            {
                case "dynamic":
                case "d":
                {
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                    Debug.Log("[InputMode] Dynamic Update");
                    return true;
                }

                case "fixed":
                case "f":
                {
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
                    Debug.Log("[InputMode] Fixed Update");
                    return true;
                }

                case "manual":
                case "m":
                {
                    InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                    Debug.Log("[InputMode] Manual Update");
                    return true;
                }

                default:
                    return false;
            }
        }
    }
}
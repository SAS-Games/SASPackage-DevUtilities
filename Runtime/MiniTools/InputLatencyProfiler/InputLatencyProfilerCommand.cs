using UnityEngine;
using UnityEngine.InputSystem;

#if ENABLE_DEBUG

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Input Latency Profiler Command", menuName = DeveloperConsole.CommandBasePath + "Input Latency Profiler")]
    public class InputLatencyProfilerCommand : CompositeConsoleCommand
    {
        [SerializeField] private GameObject m_InputLatencyProfilerPrefab;
        private GameObject _inputLatencyProfiler;
        private InputSettings.UpdateMode _previousUpdateMode;
        private bool _hasPreviousUpdateMode;
        public override string HelpText => "Usage: InputLatencyProfiler <Overlay|InputUpdateMode>.";
        

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

            if (mode == "restore" || mode == "r")
            {
                if (!_hasPreviousUpdateMode)
                    return false;

                InputSystem.settings.updateMode = _previousUpdateMode;
                _hasPreviousUpdateMode = false;
                Debug.Log($"[InputMode] Restored {_previousUpdateMode}");
                return true;
            }

            switch (mode)
            {
                case "dynamic":
                case "d":
                {
                    return ApplyUpdateMode(InputSettings.UpdateMode.ProcessEventsInDynamicUpdate, "Dynamic Update");
                }

                case "fixed":
                case "f":
                {
                    return ApplyUpdateMode(InputSettings.UpdateMode.ProcessEventsInFixedUpdate, "Fixed Update");
                }

                case "manual":
                case "m":
                {
                    return ApplyUpdateMode(InputSettings.UpdateMode.ProcessEventsManually, "Manual Update");
                }

                default:
                    return false;
            }
        }

        private bool ApplyUpdateMode(InputSettings.UpdateMode mode, string label)
        {
            if (!_hasPreviousUpdateMode)
            {
                _previousUpdateMode = InputSystem.settings.updateMode;
                _hasPreviousUpdateMode = true;
            }

            InputSystem.settings.updateMode = mode;
            Debug.Log($"[InputMode] {label}");
            return true;
        }
    }
}
#endif

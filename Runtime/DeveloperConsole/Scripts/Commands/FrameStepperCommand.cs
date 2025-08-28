using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "FrameStepperCommand", menuName = "SAS/DeveloperConsole/Commands/FrameStepper")]
    public class FrameStepperCommand : ConsoleCommand
    {
        [SerializeField] private FrameStepper m_FrameStepperPrefab;
        private FrameStepper _frameStepper;
        public override string HelpText => "Usage: FrameStepper [true/false]\n" +
                                           "Show or hide the Frame Stepper UI at runtime.\n" +
                                           "Use 'true' to enable and 'false' to disable.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            if (args != null && args.Length > 0)
            {
                if (BoolUtil.TryParse(args[0], out var isVisible))
                {
                    if (_frameStepper == null)
                        _frameStepper = Instantiate(m_FrameStepperPrefab);
                    _frameStepper.Show(isVisible);
                    return true;
                }
            }

            return false;
        }
    }
}
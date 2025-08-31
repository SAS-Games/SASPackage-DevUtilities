using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New SetTimeScale Command", menuName = "SAS/DeveloperConsole/Commands/SetTimeScale")]
    public class SetTimeScaleCommand : ConsoleCommand
    {
        public override string HelpText => "Sets the game's time scale. Usage: SetTimeScale <float>";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
            if (args.Length != 1)
            {
                Debug.LogError("Usage: SetTimeScale <float>");
                return false;
            }

            if (float.TryParse(args[0], out float timeScale))
            {
                Time.timeScale = Mathf.Max(0f, timeScale); // Prevent negative time scale
                Debug.Log($"Time scale set to {Time.timeScale}");
                return true;
            }

            Debug.LogWarning("Invalid time scale value. Please enter a valid float.");
            return false;
        }
    }
}
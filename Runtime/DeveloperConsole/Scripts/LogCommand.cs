using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Log Command", menuName = "SAS/Utilities/DeveloperConsole/Commands/Log Command")]
    public class LogCommand : ConsoleCommand
    {
        public override string HelpText => "Usage: Log [value]. Print the provided value on console.";

        public override bool Process(string[] args, DeveloperConsoleBehaviour developerConsole)
        {
            string logText = string.Join(" ", args);

            Debug.Log(logText);

            return true;
        }
    }
}

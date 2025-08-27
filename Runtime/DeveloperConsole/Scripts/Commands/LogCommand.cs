using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    [CreateAssetMenu(fileName = "New Log Command", menuName = "SAS/DeveloperConsole/Commands/Log Command")]
    public class LogCommand : ConsoleCommand
    {
        public override string HelpText => "Usage: Log [value]. Print the provided value on console.";

        public override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args)
        {
            string logText = "Temp text to show Log Command";
            if (args != null)
                logText = string.Join(" ", args);

            Debug.Log(logText);

            return true;
        }
    }
}

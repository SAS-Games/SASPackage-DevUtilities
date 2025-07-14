using SAS.Utilities.DeveloperConsole;
using TMPro;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public class ConsoleCommandButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text m_TextView;
        private DeveloperConsoleBehaviour _developerConsole;
        private IConsoleCommand _command;

        public void Init(IConsoleCommand command, DeveloperConsoleBehaviour developerConsole)
        {
            // m_TextView.text = command.CommandWord;
            // _command = command;
            // _developerConsole = developerConsole;
        }

        public void ProcessCommand()
        {
            //_command.Process(_developerConsole);
        }
    }
}
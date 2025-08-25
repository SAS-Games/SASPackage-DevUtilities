using System;
using System.Collections.Generic;
using System.Linq;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsole
    {
        public readonly string _prefix;
        private readonly CommandSuggester _commandSuggester = new();
        private readonly CommandHistory _commandHistory = new();
        public readonly IEnumerable<IConsoleCommand> ConsoleCommands;


        public DeveloperConsole(string prefix, IEnumerable<IConsoleCommand> consoleCommands)
        {
            this._prefix = prefix;
            this.ConsoleCommands = consoleCommands;
            foreach (var consoleCommand in this.ConsoleCommands)
            {
                _commandSuggester.Insert($"{this._prefix}{consoleCommand.Name}");

                foreach (var preset in consoleCommand.Presets)
                {
                    _commandSuggester.Insert($"{this._prefix}{preset}");
                }
            }
        }

        public void ProcessCommand(string inputValue, DeveloperConsoleBehaviour developerConsole)
        {
            if (!inputValue.StartsWith(_prefix))
            {
                return;
            }

            inputValue = inputValue.Remove(0, _prefix.Length);
            string[] inputSplit = inputValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string commandInput = inputSplit[0];
            string[] args = inputSplit.Skip(1).ToArray();
            if (inputValue.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                developerConsole.DisplayHelpText("");
                return;
            }

            if (ProcessCommand(commandInput, args, developerConsole))
                _commandHistory.Add(inputValue);
        }

        private bool ProcessCommand(string commandInput, string[] args, DeveloperConsoleBehaviour developerConsole)
        {
            foreach (var command in ConsoleCommands)
            {
                if (!command.Contains(commandInput))
                    continue;

                if (command.HelpRequest(commandInput, args, out var message))
                {
                    developerConsole.DisplayHelpText(message);
                    return true;
                }
                else
                {
                    if (!command.Process(developerConsole, commandInput, args))
                    {
                        developerConsole.DisplayHelpText($"Failed to execute the Command '{commandInput}'  \n{message}");
                        Debug.LogError($"Failed to execute the Command '{commandInput}' \n{message}");
                        return false;
                    }
                }
                developerConsole.DisplayHelpText($"");
                return true;
            }

            Debug.LogError($"No command found for '{commandInput}'");
            return false;
        }

        public List<string> GetCommandSuggestions(string input)
        {
            return _commandSuggester.GetAllWithPrefix(input);
        }
    }
}
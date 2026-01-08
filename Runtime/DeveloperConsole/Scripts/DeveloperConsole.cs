using System;
using System.Collections.Generic;
using System.Linq;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsole
    {
        public const string CommandBasePath = "SAS/DeveloperConsole/Commands/";
        public readonly string _prefix;
        private readonly CommandSuggester _commandSuggester = new();
        private readonly CommandHistory _commandHistory = new();
        public readonly List<IConsoleCommand> ConsoleCommands = new List<IConsoleCommand>();
        public CommandHistory CommandHistory => _commandHistory;

        public DeveloperConsole(string prefix, IEnumerable<IConsoleCommand> consoleCommands)
        {
            this._prefix = prefix;
            foreach (var consoleCommand in consoleCommands)
                AddCommand(consoleCommand);
        }

        public void ProcessCommand(string inputValue, DeveloperConsoleBehaviour developerConsole, out bool close)
        {
            close = false;
            if (!inputValue.StartsWith(_prefix))
                return;

            inputValue = inputValue.Remove(0, _prefix.Length);
            string[] inputSplit = inputValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (inputSplit.Length == 0)
                return;
            string commandInput = inputSplit[0];
            string[] args = inputSplit.Skip(1).ToArray();
            if (inputValue.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                developerConsole.DisplayHelpText("");
                return;
            }

            if (ProcessCommand(commandInput, args, developerConsole, out close))
                _commandHistory.Add(inputValue);
        }

        private bool ProcessCommand(string commandInput, string[] args, DeveloperConsoleBehaviour developerConsole,
            out bool close)
        {
            close = false;
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
                        developerConsole.DisplayHelpText(
                            $"Failed to execute the Command '{commandInput}'  \n{message}");
                        Debug.LogError($"Failed to execute the Command '{commandInput}' \n{message}");
                        return false;
                    }
                }

                developerConsole.DisplayHelpText($"");
                close = command.CloseOnCompletion;
                return true;
            }

            Debug.LogError($"No command found for '{commandInput}'");
            return false;
        }

        public List<string> GetCommandSuggestions(string input)
        {
            return _commandSuggester.GetAllWithPrefix(input);
        }

        public void AddCommand(IConsoleCommand cmd)
        {
            if (cmd == null || ConsoleCommands.Contains(cmd) || string.IsNullOrEmpty(cmd.Name))
                return;

            ConsoleCommands.Add(cmd);
            _commandSuggester.Insert($"{this._prefix}{cmd.Name}");
            foreach (var preset in cmd.Presets)
                _commandSuggester.Insert($"{this._prefix}{preset}");
        }

        public void RemoveCommand(IConsoleCommand cmd)
        {
            if (cmd == null)
                return;
            ConsoleCommands.Remove(cmd);
        }
    }
}
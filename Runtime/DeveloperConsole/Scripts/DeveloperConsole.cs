using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
        public event Action CommandsChanged;

        public DeveloperConsole(string prefix, IEnumerable<IConsoleCommand> consoleCommands)
        {
            this._prefix = prefix;
            foreach (var consoleCommand in consoleCommands)
                AddCommand(consoleCommand);
        }

        public void ProcessCommand(string inputValue, DeveloperConsoleBehaviour developerConsole, out bool close)
        {
            TryProcessCommand(inputValue, developerConsole, out close);
        }

        public bool TryProcessCommand(string inputValue, DeveloperConsoleBehaviour developerConsole, out bool close)
        {
            close = false;
            if (string.IsNullOrWhiteSpace(inputValue) || !inputValue.StartsWith(_prefix))
                return false;

            inputValue = inputValue.Remove(0, _prefix.Length);

            // This regex matches non-whitespace sequences OR text inside double quotes
            var matches = Regex.Matches(inputValue, @"[^\s""]+|""([^""]*)""");

            string[] inputSplit = matches
                .Cast<Match>()
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value)
                .ToArray();

            if (inputSplit.Length == 0)
                return false; 
            string commandInput = inputSplit[0];
            string[] args = inputSplit.Skip(1).ToArray();
            if (inputValue.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                developerConsole.DisplayHelpText("");
                return true;
            }

            if (ProcessCommand(commandInput, args, developerConsole, out close))
            {
                _commandHistory.Add(inputValue);
                return true;
            }

            return false;
        }

        private bool ProcessCommand(string commandInput, string[] args, DeveloperConsoleBehaviour developerConsole, out bool close)
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

            cmd.Init();
            ConsoleCommands.Add(cmd);
            _commandSuggester.Insert($"{this._prefix}{cmd.Name}");
            foreach (var preset in cmd.Presets)
                _commandSuggester.Insert($"{this._prefix}{preset}");
            CommandsChanged?.Invoke();
        }

        public void RemoveCommand(IConsoleCommand cmd)
        {
            if (cmd == null)
                return;

            if (!ConsoleCommands.Remove(cmd))
                return;

            _commandSuggester.Remove($"{this._prefix}{cmd.Name}");

            if (cmd.Presets != null)
            {
                foreach (var preset in cmd.Presets)
                    _commandSuggester.Remove($"{this._prefix}{preset}");
            }

            CommandsChanged?.Invoke();
        }
    }
}

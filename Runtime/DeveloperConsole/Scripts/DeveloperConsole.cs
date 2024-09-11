using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public class DeveloperConsole
    {
        private readonly string prefix;
        private readonly IEnumerable<IConsoleCommand> commands;

        public DeveloperConsole(string prefix, IEnumerable<IConsoleCommand> commands)
        {
            this.prefix = prefix;
            this.commands = commands;
        }

        public void ProcessCommand(string inputValue, DeveloperConsoleBehaviour developerConsole)
        {
            if (!inputValue.StartsWith(prefix)) { return; }

            inputValue = inputValue.Remove(0, prefix.Length);
            string[] inputSplit = inputValue.Split(' ');

            string commandInput = inputSplit[0];
            string[] args = inputSplit.Skip(1).ToArray();
            if (inputValue.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                developerConsole.DisplayHelpText("");
                return; 
            }
            // If the input ends with "help", show the command's help text
            if (args.Length > 0 && args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelpText(commandInput, developerConsole);
                return;
            }

            ProcessCommand(commandInput, args, developerConsole);
        }

        private void ShowHelpText(string commandInput, DeveloperConsoleBehaviour developerConsole)
        {
            var command = commands.FirstOrDefault(c => c.CommandWord.Equals(commandInput, StringComparison.OrdinalIgnoreCase));

            if (command != null)
            {
                developerConsole.DisplayHelpText(command.HelpText);  // Method to display help text in UI
            }
            else
            {
                developerConsole.DisplayHelpText($"No command found for '{commandInput}'");
            }
        }


        public string GetCommandSuggestion(string input)
        {
            // Remove the prefix before filtering commands
            if (input.StartsWith(prefix))
            {
                input = input.Remove(0, prefix.Length);
            }

            // Step 1: Try to find a command that starts with the input
            var exactMatch = commands
                .Where(c => c.CommandWord.StartsWith(input, System.StringComparison.OrdinalIgnoreCase))
                .Select(c => c.CommandWord)
                .FirstOrDefault();

            if (exactMatch != null)
            {
                return exactMatch;
            }

            // Step 2: If no exact match, find the command with the most matching characters
            return commands
                .OrderByDescending(c => GetMatchingCharactersCount(c.CommandWord, input))
                .Select(c => c.CommandWord)
                .FirstOrDefault();
        }

        private int GetMatchingCharactersCount(string command, string input)
        {
            int matchingCharacters = 0;
            int minLength = Mathf.Min(command.Length, input.Length);

            // Count the number of matching characters from the start
            for (int i = 0; i < minLength; i++)
            {
                if (command[i] == input[i])
                {
                    matchingCharacters++;
                }
            }

            return matchingCharacters;
        }


        public void ProcessCommand(string commandInput, string[] args, DeveloperConsoleBehaviour developerConsole)
        {
            foreach (var command in commands)
            {
                if (!commandInput.Equals(command.CommandWord, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (command.Process(args, developerConsole))
                {
                    return;
                }
            }
        }
    }
}

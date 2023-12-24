using System;
using System.Collections.Generic;
using System.Linq;

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

            ProcessCommand(commandInput, args, developerConsole);
        }

        public void ProcessCommand(string commandInput, string[] args, DeveloperConsoleBehaviour developerConsole)
        {
            foreach (var command in commands)
            {
                if (!commandInput.Equals(command.CommandWord, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (command.Process(args,developerConsole))
                {
                    return;
                }
            }
        }
    }
}

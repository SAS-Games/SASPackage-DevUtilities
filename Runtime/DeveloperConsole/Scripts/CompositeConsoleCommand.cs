using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace SAS.Utilities.DeveloperConsole
{
    public abstract class CompositeConsoleCommand : ConsoleCommand
    {
        [Serializable]
        public class BoolResultUnityEvent : UnityEvent<string[], CommandResult>
        {
        }

        public class CommandResult
        {
            public bool Success;
            public string Message; //todo: need to set this, yet to be evaluated the need of custom message 
        }

        [Serializable]
        private class SubCommand
        {
            public string Name;
            public string HelpText;
            public string[] Presets;
            public UnityEvent<string[], CommandResult> Action;
        }

        [FormerlySerializedAs("subCommands")]
        [SerializeField]
        private List<SubCommand> m_SubCommands = new();

        public override bool HelpRequest(string command, string[] args, out string message)
        {
            var fullCommand = command.Split(".");
            if (fullCommand.Length <= 1)
                return base.HelpRequest(command, args, out message);

            string subCommand = fullCommand[1];

            var sub = m_SubCommands.Find(s => s.Name.Equals(subCommand, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
            {
                message = $"Subcommand: '{subCommand} under Command: {command}' not found.";
                return true;
            }
            else
            {
                message = sub.HelpText;
                return args.Length > 0 && args[0].Equals("help", StringComparison.OrdinalIgnoreCase);
            }
        }

        public sealed override bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null)
        {
            var splitValues = command.Split(".");
            if (splitValues.Length <= 1)
                return false;

            string subCommand = splitValues[1];

            var sub = m_SubCommands.Find(s => s.Name.Equals(subCommand, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
                return false;

            var result = new CommandResult();
            sub.Action.Invoke(args, result);
            return result.Success;
        }

        public sealed override string[] Presets
        {
            get
            {
                List<string> presets = new();
                presets.AddRange(base.Presets);
                foreach (var s in m_SubCommands)
                {
                    presets.Add($"{Name}.{s.Name}");
                    if (s.Presets != null)
                    {
                        foreach (var preset in s.Presets)
                        {
                            presets.Add($"{Name}.{preset}");
                        }
                    }
                }

                return presets.ToArray();
            }
        }

        public override bool Contains(string commandName)
        {
            var commandSplit = commandName.Trim().Split(".");
            return base.Contains(commandSplit[0]);
        }
    }
}
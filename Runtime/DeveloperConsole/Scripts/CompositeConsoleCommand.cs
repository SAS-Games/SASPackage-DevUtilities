using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SAS.Utilities.DeveloperConsole
{
    public abstract class CompositeConsoleCommand : ConsoleCommand
    {

        [Serializable]
        protected class SubCommand
        {
            public string Name;
            public string HelpText;
            public string[] Presets;
            [NonSerialized] public Func<string[], bool> Action;
        }

        [FormerlySerializedAs("subCommands")]
        [SerializeField] protected List<SubCommand> m_SubCommands = new();

        protected void Register(string name, Func<string[], bool> action)
        {
            var sub = m_SubCommands.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (sub == null)
            {
                Debug.LogWarning($"No SubCommand metadata for '{name}', under the command config '{this.name}'.");
                return;
            }
            sub.Action = action;
        }

        private void OnEnable()
        {
            foreach (var sub in m_SubCommands)
                sub.Action = null;
            CommandMethodRegistry();
        }

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
            if (sub == null || sub.Action == null)
                return false;

            var result = sub.Action.Invoke(args);
            return result;
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

        protected abstract void CommandMethodRegistry();
    }
}
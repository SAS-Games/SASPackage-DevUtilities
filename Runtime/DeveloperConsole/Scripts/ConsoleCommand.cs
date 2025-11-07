using System;
using UnityEngine;

namespace SAS.Utilities.DeveloperConsole
{
    public abstract class ConsoleCommand : ScriptableObject, IConsoleCommand
    {
        [field: SerializeField] public string Name { get; private set; }
        [SerializeField] private string[] m_Presets;
        public abstract string HelpText { get; }
        public virtual string[] Presets => m_Presets;

        [field: SerializeField] public bool CloseOnCompletion { get; private set; } = true;

        public virtual bool HelpRequest(string command, string[] args, out string message)
        {
            message = HelpText;
            return args.Length > 0 && args[0].Equals("help", StringComparison.OrdinalIgnoreCase);
        }

        public virtual bool Contains(string commandName)
        {
            return commandName.Equals(Name, StringComparison.OrdinalIgnoreCase);
        }

        public abstract bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null);
    }
}
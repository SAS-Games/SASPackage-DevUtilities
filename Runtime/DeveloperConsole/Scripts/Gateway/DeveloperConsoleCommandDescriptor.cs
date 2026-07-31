using System;

namespace SAS.Utilities.DeveloperConsole
{
    [Serializable]
    public sealed class DeveloperConsoleCommandDescriptor
    {
        public DeveloperConsoleCommandDescriptor(string name, string helpText, string[] presets, bool closeOnCompletion)
        {
            Name = name ?? string.Empty;
            HelpText = helpText ?? string.Empty;
            Presets = presets ?? Array.Empty<string>();
            CloseOnCompletion = closeOnCompletion;
        }

        public string Name { get; }
        public string HelpText { get; }
        public string[] Presets { get; }
        public bool CloseOnCompletion { get; }
    }
}

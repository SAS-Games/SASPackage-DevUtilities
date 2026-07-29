using System;

namespace SAS.Utilities.DeveloperConsole
{
    internal sealed class GatewayConsoleCommandProxy : IConsoleCommand
    {
        private readonly DeveloperConsoleCommandDescriptor _descriptor;
        private readonly IDeveloperConsoleCommandGateway _gateway;

        public GatewayConsoleCommandProxy(
            DeveloperConsoleCommandDescriptor descriptor,
            IDeveloperConsoleCommandGateway gateway)
        {
            _descriptor = descriptor;
            _gateway = gateway;
        }

        public string Name => _descriptor.Name;
        public string[] Presets => _descriptor.Presets ?? Array.Empty<string>();
        public string HelpText => _descriptor.HelpText ?? string.Empty;
        public bool CloseOnCompletion => _descriptor.CloseOnCompletion;

        public void Init()
        {
        }

        public bool HelpRequest(
            string command,
            string[] args,
            out string message)
        {
            message = HelpText;
            return args != null &&
                   args.Length > 0 &&
                   args[0].Equals("help", StringComparison.OrdinalIgnoreCase);
        }

        public bool Process(
            DeveloperConsoleBehaviour developerConsole,
            string command,
            string[] args = null)
        {
            string commandLine = command;
            if (args != null && args.Length > 0)
                commandLine += " " + string.Join(" ", args);

            _gateway.Execute(commandLine);
            return true;
        }

        public bool Contains(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName) ||
                string.IsNullOrWhiteSpace(Name))
                return false;

            string root = commandName.Split('.')[0];
            return root.Equals(Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}

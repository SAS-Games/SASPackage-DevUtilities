using System;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal sealed class EditorRemoteCommandPresentationGateway : IDeveloperConsoleCommandGateway, IDisposable
    {
        private readonly RemoteDevUtilitiesClient _client;
        private RemoteCommandDescriptor[] _lastCatalog;
        private string _lastPrefix;
        private RemoteCommandExecuteResponse _lastResult;
        private DeveloperConsoleCommandDescriptor[] _commands;

        public EditorRemoteCommandPresentationGateway(RemoteDevUtilitiesClient client)
        {
            _client = client;
            _lastCatalog = client.Commands.Commands;
            _lastPrefix = client.Commands.Prefix;
            _lastResult = client.Commands.LastResult;
            _commands = MapCommands(_lastCatalog);
            _client.StateChanged += OnClientStateChanged;
            if (_client.IsConnected)
                _client.Commands.RequestCatalog();
        }

        public bool IsConnected => _client.IsConnected;
        public string Prefix => _client.Commands.Prefix;
        public DeveloperConsoleCommandDescriptor[] Commands => _commands;

        public event Action CatalogChanged;
        public event Action<DeveloperConsoleCommandResult> CommandCompleted;

        public void Execute(string commandLine)
        {
            _client.CommandPresentation.Execute(commandLine);
        }

        public void Dispose()
        {
            _client.StateChanged -= OnClientStateChanged;
            CatalogChanged = null;
            CommandCompleted = null;
        }

        private void OnClientStateChanged()
        {
            RemoteCommandDescriptor[] catalog = _client.Commands.Commands;
            string prefix = _client.Commands.Prefix;
            if (!ReferenceEquals(_lastCatalog, catalog) || !string.Equals(_lastPrefix, prefix, StringComparison.Ordinal))
            {
                _lastCatalog = catalog;
                _lastPrefix = prefix;
                _commands = MapCommands(catalog);
                CatalogChanged?.Invoke();
            }

            RemoteCommandExecuteResponse result = _client.Commands.LastResult;
            if (!ReferenceEquals(_lastResult, result))
            {
                _lastResult = result;
                if (result != null)
                {
                    CommandCompleted?.Invoke(new DeveloperConsoleCommandResult(result.Success, result.CloseRequested, result.Message));
                }
            }
        }

        private static DeveloperConsoleCommandDescriptor[] MapCommands(RemoteCommandDescriptor[] commands)
        {
            if (commands == null || commands.Length == 0)
                return Array.Empty<DeveloperConsoleCommandDescriptor>();

            var mapped = new DeveloperConsoleCommandDescriptor[commands.Length];
            for (int i = 0; i < commands.Length; i++)
            {
                RemoteCommandDescriptor command = commands[i];
                mapped[i] = command == null ? null : new DeveloperConsoleCommandDescriptor(command.Name, command.HelpText, command.Presets, command.CloseOnCompletion);
            }

            return mapped;
        }
    }
}

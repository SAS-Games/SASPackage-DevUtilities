using System;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;

namespace SAS.Utilities.RemoteDevUtilities.Editor.DebugHost
{
    internal sealed class EditorRemoteCommandPresentationGateway : IDeveloperConsoleCommandGateway, IDisposable
    {
        private readonly RemoteDevUtilitiesClient _client;
        private readonly RemoteCommandClient _commandClient;
        private readonly RemoteCommandPresentationCoordinator _coordinator;
        private RemoteCommandDescriptor[] _lastCatalog;
        private string _lastPrefix;
        private RemoteCommandExecuteResponse _lastResult;
        private DeveloperConsoleCommandDescriptor[] _commands;

        public EditorRemoteCommandPresentationGateway(RemoteDevUtilitiesClient client)
        {
            _client = client;
            _commandClient = client.GetRequiredFeature<RemoteCommandClient>();
            _coordinator = new RemoteCommandPresentationCoordinator(client);
            _lastCatalog = _commandClient.Commands;
            _lastPrefix = _commandClient.Prefix;
            _lastResult = _commandClient.LastResult;
            _commands = MapCommands(_lastCatalog);
            _client.StateChanged += OnClientStateChanged;
            if (_client.IsConnected)
                _commandClient.RequestCatalog();
        }

        public bool IsConnected => _client.IsConnected;
        public string Prefix => _commandClient.Prefix;
        public DeveloperConsoleCommandDescriptor[] Commands => _commands;

        public event Action CatalogChanged;
        public event Action<DeveloperConsoleCommandResult> CommandCompleted;

        public void Execute(string commandLine)
        {
            _coordinator.Execute(commandLine);
        }

        public void Dispose()
        {
            _client.StateChanged -= OnClientStateChanged;
            CatalogChanged = null;
            CommandCompleted = null;
        }

        private void OnClientStateChanged()
        {
            RemoteCommandDescriptor[] catalog = _commandClient.Commands;
            string prefix = _commandClient.Prefix;
            if (!ReferenceEquals(_lastCatalog, catalog) || !string.Equals(_lastPrefix, prefix, StringComparison.Ordinal))
            {
                _lastCatalog = catalog;
                _lastPrefix = prefix;
                _commands = MapCommands(catalog);
                CatalogChanged?.Invoke();
            }

            RemoteCommandExecuteResponse result = _commandClient.LastResult;
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

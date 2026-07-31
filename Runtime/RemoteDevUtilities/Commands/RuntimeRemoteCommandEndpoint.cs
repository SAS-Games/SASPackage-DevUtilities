using System;
using System.Collections.Generic;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using RuntimeConsole = SAS.Utilities.DeveloperConsole.DeveloperConsole;

namespace SAS.Utilities.RemoteDevUtilities.Commands
{
    internal sealed class RuntimeRemoteCommandEndpoint : IRuntimeRemoteEndpoint, IRuntimeRemoteSessionListener
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.CommandCatalogRequest,
            RemoteMessageTypes.CommandExecuteRequest
        };

        private RuntimeRemoteEndpointContext _context;
        private DeveloperConsoleBehaviour _behaviour;
        private bool _catalogDirty;
        private bool _remoteSessionActive;

        public IEnumerable<string> MessageTypes => SupportedMessages;

        public void Initialize(RuntimeRemoteEndpointContext context)
        {
            _context = context;
            EnsureConsoleSubscription();
        }

        public void Handle(RemoteEnvelope envelope)
        {
            switch (envelope.MessageType)
            {
                case RemoteMessageTypes.CommandCatalogRequest:
                    SendCatalog(envelope.RequestId);
                    break;
                case RemoteMessageTypes.CommandExecuteRequest:
                    Execute(envelope);
                    break;
            }
        }

        public void Tick()
        {
            EnsureConsoleSubscription();
            if (!_remoteSessionActive || !_catalogDirty)
                return;

            SendCatalog(0);
        }

        public void Dispose()
        {
            SetConsoleBehaviour(null);
            _remoteSessionActive = false;
            _catalogDirty = false;
            _context = null;
        }

        public void OnRemoteSessionStateChanged(bool active)
        {
            _remoteSessionActive = active;
            if (!active)
                _catalogDirty = false;
        }

        private void SendCatalog(long requestId)
        {
            DeveloperConsoleBehaviour behaviour = RuntimeDeveloperConsoleProvider.GetOrCreate();
            SetConsoleBehaviour(behaviour);
            if (behaviour == null)
            {
                _context.Sender.Send(RemoteMessageTypes.CommandCatalogResponse, requestId, new RemoteCommandCatalogResponse
                {
                    Error = "The runtime Developer Console has not been created."
                });
                _catalogDirty = false;
                return;
            }

            RuntimeConsole console = behaviour.DeveloperConsole;
            var descriptors = new List<RemoteCommandDescriptor>(console.ConsoleCommands.Count);
            foreach (IConsoleCommand command in console.ConsoleCommands)
            {
                if (command == null || string.IsNullOrWhiteSpace(command.Name))
                    continue;

                descriptors.Add(new RemoteCommandDescriptor
                {
                    Name = command.Name,
                    HelpText = command.HelpText ?? string.Empty,
                    Presets = command.Presets ?? Array.Empty<string>(),
                    CloseOnCompletion = command.CloseOnCompletion
                });
            }

            _context.Sender.Send(RemoteMessageTypes.CommandCatalogResponse, requestId, new RemoteCommandCatalogResponse
            {
                Available = true,
                Prefix = console._prefix ?? string.Empty,
                Commands = descriptors.ToArray()
            });
            _catalogDirty = false;
        }

        private void EnsureConsoleSubscription()
        {
            DeveloperConsoleBehaviour current = DeveloperConsoleBehaviour.Instance;
            if (current == null && _behaviour == null)
                current = RuntimeDeveloperConsoleProvider.GetOrCreate();

            SetConsoleBehaviour(current);
        }

        private void SetConsoleBehaviour(DeveloperConsoleBehaviour behaviour)
        {
            if (_behaviour == behaviour)
                return;

            if (_behaviour != null)
                _behaviour.CommandsChanged -= OnCommandsChanged;

            _behaviour = behaviour;

            if (_behaviour != null)
            {
                _ = _behaviour.DeveloperConsole;
                _behaviour.CommandsChanged += OnCommandsChanged;
            }
        }

        private void OnCommandsChanged()
        {
            _catalogDirty = true;
        }

        private void Execute(RemoteEnvelope envelope)
        {
            if (!_context.Settings.AllowCommandExecution)
            {
                SendExecutionResult(envelope.RequestId, false, false, "Remote command execution is disabled.");
                return;
            }

            if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteCommandExecuteRequest request, out string error))
            {
                SendExecutionResult(envelope.RequestId, false, false, error);
                return;
            }

            DeveloperConsoleBehaviour behaviour = RuntimeDeveloperConsoleProvider.GetOrCreate();
            if (behaviour == null)
            {
                SendExecutionResult(envelope.RequestId, false, false, "The runtime Developer Console has not been created.");
                return;
            }

            RuntimeConsole console = behaviour.DeveloperConsole;
            string commandLine = request.CommandLine?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(console._prefix) && !commandLine.StartsWith(console._prefix, StringComparison.Ordinal))
                commandLine = console._prefix + commandLine;

            string output = null;

            void CaptureOutput(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    output = value;
            }

            behaviour.HelpTextDisplayed += CaptureOutput;
            try
            {
                bool success = console.TryProcessCommand(commandLine, behaviour, out bool close);
                SendExecutionResult(envelope.RequestId, success, close, output ?? (success ? "Command completed." : "Command failed."));
            }
            catch (Exception exception)
            {
                SendExecutionResult(envelope.RequestId, false, false, exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                behaviour.HelpTextDisplayed -= CaptureOutput;
            }
        }

        private void SendExecutionResult(long requestId, bool success, bool close, string message)
        {
            _context.Sender.Send(RemoteMessageTypes.CommandExecuteResponse, requestId, new RemoteCommandExecuteResponse
            {
                Success = success,
                CloseRequested = close,
                Message = message
            });
        }
    }
}

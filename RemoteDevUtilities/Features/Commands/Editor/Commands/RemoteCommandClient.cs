using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands
{
    [RemoteEditorFeature("commands", 100)]
    internal sealed class RemoteCommandClient : IRemoteCommandExecutor
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteCommandMessageTypes.CatalogResponse,
            RemoteCommandMessageTypes.ExecuteResponse
        };

        private readonly IRemoteEditorSession _session;

        public RemoteCommandClient(IRemoteEditorSession session)
        {
            _session = session;
        }

        public IEnumerable<string> MessageTypes => SupportedMessages;
        public RemoteCommandDescriptor[] Commands { get; private set; } = Array.Empty<RemoteCommandDescriptor>();
        public string Prefix { get; private set; } = string.Empty;
        public string Error { get; private set; }
        public RemoteCommandExecuteResponse LastResult { get; private set; }
        public long LastResultRequestId { get; private set; }
        internal event Action<long, RemoteCommandExecuteResponse> ExecutionCompleted;
        internal event Action CatalogChanged;
        RemoteCommandExecutionResult IRemoteCommandExecutor.ExecutionResult => LastResult == null
            ? null
            : new RemoteCommandExecutionResult(LastResult.Success, LastResult.CloseRequested, LastResult.Message);
        long IRemoteCommandExecutor.ExecutionResultRequestId => LastResultRequestId;

        public bool HasCommand(string commandName)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                return false;
            foreach (RemoteCommandDescriptor command in Commands)
            {
                if (string.Equals(command?.Name, commandName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public void RequestCatalog()
        {
            Error = null;
            _session.Send(RemoteCommandMessageTypes.CatalogRequest, new RemoteCommandCatalogRequest());
        }

        public void OnConnected() => RequestCatalog();

        public long Execute(string commandLine)
        {
            LastResult = null;
            LastResultRequestId = 0;
            return _session.Send(RemoteCommandMessageTypes.ExecuteRequest, new RemoteCommandExecuteRequest { CommandLine = commandLine });
        }

        internal void CompleteLocally(RemoteCommandExecuteResponse response)
        {
            LastResult = response;
            LastResultRequestId = 0;
            ExecutionCompleted?.Invoke(0, response);
            _session.NotifyStateChanged();
        }

        public void Handle(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteCommandMessageTypes.CatalogResponse)
            {
                if (!RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteCommandCatalogResponse response, out string error))
                {
                    Error = error;
                }
                else
                {
                    Commands = response.Commands ?? Array.Empty<RemoteCommandDescriptor>();
                    Prefix = response.Prefix ?? string.Empty;
                    Error = response.Available ? null : response.Error;
                }
                CatalogChanged?.Invoke();
            }
            else if (envelope.MessageType == RemoteCommandMessageTypes.ExecuteResponse)
            {
                if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteCommandExecuteResponse response, out string error))
                    LastResult = response;
                else
                    LastResult = new RemoteCommandExecuteResponse { Message = error };
                LastResultRequestId = envelope.RequestId;
                ExecutionCompleted?.Invoke(LastResultRequestId, LastResult);
            }

            _session.NotifyStateChanged();
        }

        public void Reset()
        {
            Commands = Array.Empty<RemoteCommandDescriptor>();
            Prefix = string.Empty;
            Error = null;
            LastResult = null;
            LastResultRequestId = 0;
        }
    }
}

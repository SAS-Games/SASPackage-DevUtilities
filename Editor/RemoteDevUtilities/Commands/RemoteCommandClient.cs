using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Commands
{
    internal sealed class RemoteCommandClient : IRemoteEditorFeatureClient
    {
        private static readonly string[] SupportedMessages =
        {
            RemoteMessageTypes.CommandCatalogResponse,
            RemoteMessageTypes.CommandExecuteResponse
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

        public void RequestCatalog()
        {
            Error = null;
            _session.Send(RemoteMessageTypes.CommandCatalogRequest, new RemoteCommandCatalogRequest());
        }

        public long Execute(string commandLine)
        {
            LastResult = null;
            LastResultRequestId = 0;
            return _session.Send(RemoteMessageTypes.CommandExecuteRequest, new RemoteCommandExecuteRequest { CommandLine = commandLine });
        }

        internal void CompleteLocally(RemoteCommandExecuteResponse response)
        {
            LastResult = response;
            LastResultRequestId = 0;
            _session.NotifyStateChanged();
        }

        public void Handle(RemoteEnvelope envelope)
        {
            if (envelope.MessageType == RemoteMessageTypes.CommandCatalogResponse)
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
            }
            else if (envelope.MessageType == RemoteMessageTypes.CommandExecuteResponse)
            {
                if (RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteCommandExecuteResponse response, out string error))
                    LastResult = response;
                else
                    LastResult = new RemoteCommandExecuteResponse { Message = error };
                LastResultRequestId = envelope.RequestId;
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

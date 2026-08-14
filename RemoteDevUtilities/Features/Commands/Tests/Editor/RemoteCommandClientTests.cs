using NUnit.Framework;
using SAS.Utilities.DeveloperConsole;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;
using RuntimeConsole = SAS.Utilities.DeveloperConsole.DeveloperConsole;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteCommandClientTests
    {
        [Test]
        public void CommandRequest_RoundTripsThroughEnvelope()
        {
            byte[] data = RemoteProtocolSerializer.Serialize(
                RemoteCommandMessageTypes.ExecuteRequest,
                42,
                "editor-session",
                new RemoteCommandExecuteRequest { CommandLine = "Stats.Memory" });

            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(
                data, out RemoteEnvelope envelope, out string error), Is.True, error);
            Assert.That(envelope.ProtocolVersion, Is.EqualTo(RemoteProtocolConstants.Version));
            Assert.That(envelope.MessageType, Is.EqualTo(RemoteCommandMessageTypes.ExecuteRequest));
            Assert.That(envelope.RequestId, Is.EqualTo(42));
            Assert.That(envelope.SessionId, Is.EqualTo("editor-session"));
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(
                envelope, out RemoteCommandExecuteRequest request, out error), Is.True, error);
            Assert.That(request.CommandLine, Is.EqualTo("Stats.Memory"));
        }

        [Test]
        public void CommandLineParser_RemovesOptionalConsolePrefix()
        {
            Assert.That(RemoteCommandPresentationCoordinator.TryParseCommandLine(
                "/GameInfo On", "/", out string commandName, out string[] arguments), Is.True);
            Assert.That(commandName, Is.EqualTo("GameInfo"));
            Assert.That(arguments, Is.EqualTo(new[] { "On" }));
        }

        [Test]
        public void ExecuteResult_PreservesItsRequestId()
        {
            var session = new StubSession { NextRequestId = 42 };
            var client = new RemoteCommandClient(session);

            long requestId = client.Execute("Logging.ClearTags");

            Assert.That(requestId, Is.EqualTo(42));
            Assert.That(session.LastMessageType, Is.EqualTo(RemoteCommandMessageTypes.ExecuteRequest));
            Assert.That(((RemoteCommandExecuteRequest)session.LastPayload).CommandLine, Is.EqualTo("Logging.ClearTags"));

            client.Handle(new RemoteEnvelope
            {
                MessageType = RemoteCommandMessageTypes.ExecuteResponse,
                RequestId = requestId,
                PayloadJson = JsonUtility.ToJson(new RemoteCommandExecuteResponse
                {
                    Success = true,
                    Message = "Command completed."
                })
            });

            Assert.That(client.LastResultRequestId, Is.EqualTo(requestId));
            Assert.That(client.LastResult.Success, Is.True);
            Assert.That(client.LastResult.Message, Is.EqualTo("Command completed."));
            Assert.That(session.StateChangeCount, Is.EqualTo(1));
        }

        [Test]
        public void CatalogPush_ReplacesCommandsAndNotifiesEditorSurfaces()
        {
            var session = new StubSession();
            var client = new RemoteCommandClient(session);

            client.Handle(new RemoteEnvelope
            {
                MessageType = RemoteCommandMessageTypes.CatalogResponse,
                RequestId = 0,
                PayloadJson = JsonUtility.ToJson(new RemoteCommandCatalogResponse
                {
                    Available = true,
                    Prefix = "/",
                    Commands = new[]
                    {
                        new RemoteCommandDescriptor
                        {
                            Name = "SceneCommand"
                        }
                    }
                })
            });

            Assert.That(client.Commands, Has.Length.EqualTo(1));
            Assert.That(client.Commands[0].Name, Is.EqualTo("SceneCommand"));
            Assert.That(client.Prefix, Is.EqualTo("/"));
            Assert.That(session.StateChangeCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeConsole_AddAndRemoveCommand_RaiseCatalogChanges()
        {
            var console = new RuntimeConsole(string.Empty, System.Array.Empty<IConsoleCommand>());
            var command = new StubCommand("SceneCommand");
            int changeCount = 0;
            console.CommandsChanged += () => changeCount++;

            console.AddCommand(command);
            console.RemoveCommand(command);

            Assert.That(changeCount, Is.EqualTo(2));
            Assert.That(console.ConsoleCommands, Is.Empty);
        }

        private sealed class StubSession : IRemoteEditorSession
        {
            internal long NextRequestId;
            internal string LastMessageType;
            internal object LastPayload;
            internal int StateChangeCount;

            public bool IsConnected => true;

            public long Send<T>(string messageType, T payload)
            {
                LastMessageType = messageType;
                LastPayload = payload;
                return NextRequestId;
            }

            public void NotifyStateChanged() => StateChangeCount++;
        }

        private sealed class StubCommand : IConsoleCommand
        {
            internal StubCommand(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public string[] Presets => System.Array.Empty<string>();
            public string HelpText => string.Empty;
            public bool CloseOnCompletion => false;

            public void Init()
            {
            }

            public bool HelpRequest(string command, string[] args, out string message)
            {
                message = string.Empty;
                return false;
            }

            public bool Process(DeveloperConsoleBehaviour developerConsole, string command, string[] args = null) => true;

            public bool Contains(string commandName) => string.Equals(Name, commandName);
        }
    }
}

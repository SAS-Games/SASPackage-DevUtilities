using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteConnectionAuthenticationTests
    {
        private sealed class RecordingSender : IRuntimeRemoteSender
        {
            public string MessageType { get; private set; }
            public object Payload { get; private set; }
            public bool RequiresAccessToken { get; set; }

            public void Send<T>(string messageType, long requestId, T payload)
            {
                MessageType = messageType;
                Payload = payload;
            }
        }

        private RemoteDevUtilitiesRuntimeSettings _settings;
        private RuntimeConnectionEndpoint _endpoint;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            var serializedSettings = new SerializedObject(_settings);
            serializedSettings.FindProperty("m_TcpAccessToken").stringValue = "expected-token";
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            _endpoint?.Dispose();
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void HandshakeRejectsIncorrectAccessToken()
        {
            var sender = new RecordingSender
            {
                RequiresAccessToken = true
            };
            _endpoint = new RuntimeConnectionEndpoint();
            _endpoint.Initialize(new RuntimeRemoteEndpointContext
            {
                Sender = sender,
                RuntimeSessionId = "runtime-session",
                Settings = _settings
            });

            RemoteEnvelope envelope = CreateHandshake("incorrect-token");
            _endpoint.Handle(envelope);

            Assert.That(sender.MessageType, Is.EqualTo(RemoteMessageTypes.HandshakeResponse));
            Assert.That(sender.Payload, Is.TypeOf<RemoteHandshakeResponse>());
            var response = (RemoteHandshakeResponse)sender.Payload;
            Assert.That(response.Accepted, Is.False);
            Assert.That(response.Error, Does.Contain("access token"));
            Assert.That(_endpoint.IsSessionAccepted, Is.False);
        }

        [Test]
        public void PlayerConnectionHandshakeDoesNotRequireTcpAccessToken()
        {
            var sender = new RecordingSender
            {
                RequiresAccessToken = false
            };
            _endpoint = new RuntimeConnectionEndpoint();
            _endpoint.Initialize(new RuntimeRemoteEndpointContext
            {
                Sender = sender,
                RuntimeSessionId = "runtime-session",
                Settings = _settings
            });

            RemoteEnvelope envelope = CreateHandshake(string.Empty);
            _endpoint.Handle(envelope);

            var response = (RemoteHandshakeResponse)sender.Payload;
            Assert.That(response.Accepted, Is.True);
            Assert.That(_endpoint.IsSessionAccepted, Is.True);
        }

        [Test]
        public void RejectedHandshakeDoesNotEndAcceptedSession()
        {
            var sender = new RecordingSender
            {
                RequiresAccessToken = false
            };
            _endpoint = new RuntimeConnectionEndpoint();
            _endpoint.Initialize(new RuntimeRemoteEndpointContext
            {
                Sender = sender,
                RuntimeSessionId = "runtime-session",
                Settings = _settings
            });
            _endpoint.Handle(CreateHandshake(string.Empty));
            Assert.That(_endpoint.IsSessionAccepted, Is.True);

            sender.RequiresAccessToken = true;
            _endpoint.Handle(CreateHandshake("incorrect-token"));

            var response = (RemoteHandshakeResponse)sender.Payload;
            Assert.That(response.Accepted, Is.False);
            Assert.That(_endpoint.IsSessionAccepted, Is.True);
            Assert.That(_endpoint.AcceptedEditorSessionId, Is.EqualTo("editor-session"));
        }

        private static RemoteEnvelope CreateHandshake(string token)
        {
            byte[] data = RemoteProtocolSerializer.Serialize(RemoteMessageTypes.HandshakeRequest, 1, "editor-session", new RemoteHandshakeRequest
            {
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                EditorSessionId = "editor-session",
                AccessToken = token
            });

            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out string error), Is.True, error);
            return envelope;
        }
    }
}

using System;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;
using SAS.Utilities.RemoteDevUtilities.Transport;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RuntimeMultiplexedTransportTests
    {
        private sealed class FakeTransport : IRuntimeRemoteTransport
        {
            public event Action<RemoteEnvelope> MessageReceived;
            public event Action<int> EditorConnected;
            public event Action<int> EditorDisconnected;

            public bool RequiresAccessToken { get; set; }
            public int SendCount { get; private set; }

            public void Start()
            {
            }

            public void Tick()
            {
            }

            public void Send<T>(string messageType, long requestId, T payload)
            {
                SendCount++;
            }

            public void Dispose()
            {
            }

            public void RaiseHandshake()
            {
                MessageReceived?.Invoke(new RemoteEnvelope
                {
                    MessageType = RemoteMessageTypes.HandshakeRequest
                });
            }

            public void RaiseDisconnected() => EditorDisconnected?.Invoke(0);

            public void RaiseConnected() => EditorConnected?.Invoke(0);
        }

        [Test]
        public void RoutesResponsesAndAuthenticationToHandshakeTransport()
        {
            var playerConnection = new FakeTransport();
            var tcp = new FakeTransport
            {
                RequiresAccessToken = true
            };
            var transport = new RuntimeMultiplexedTransport(playerConnection, tcp);
            transport.MessageReceived += _ => transport.Send(RemoteMessageTypes.HandshakeResponse, 1, new RemoteHandshakeResponse
            {
                Accepted = true
            });
            transport.Start();

            tcp.RaiseHandshake();

            Assert.That(transport.RequiresAccessToken, Is.True);
            Assert.That(tcp.SendCount, Is.EqualTo(1));
            Assert.That(playerConnection.SendCount, Is.Zero);

            playerConnection.RaiseHandshake();

            Assert.That(transport.RequiresAccessToken, Is.False);
            Assert.That(playerConnection.SendCount, Is.EqualTo(1));
            Assert.That(tcp.SendCount, Is.EqualTo(1));
            transport.Dispose();
        }

        [Test]
        public void IgnoresDisconnectFromInactiveTransport()
        {
            var playerConnection = new FakeTransport();
            var tcp = new FakeTransport();
            var transport = new RuntimeMultiplexedTransport(playerConnection, tcp);
            int disconnectCount = 0;
            transport.EditorDisconnected += _ => disconnectCount++;
            transport.MessageReceived += _ => transport.Send(RemoteMessageTypes.HandshakeResponse, 1, new RemoteHandshakeResponse
            {
                Accepted = true
            });
            transport.Start();

            playerConnection.RaiseHandshake();
            tcp.RaiseDisconnected();
            Assert.That(disconnectCount, Is.Zero);

            playerConnection.RaiseDisconnected();
            Assert.That(disconnectCount, Is.EqualTo(1));
            transport.Dispose();
        }

        [Test]
        public void RejectedHandshakeDoesNotReplaceActiveTransport()
        {
            var playerConnection = new FakeTransport();
            var tcp = new FakeTransport();
            var transport = new RuntimeMultiplexedTransport(playerConnection, tcp);
            bool acceptHandshake = true;
            transport.MessageReceived += _ => transport.Send(RemoteMessageTypes.HandshakeResponse, 1, new RemoteHandshakeResponse
            {
                Accepted = acceptHandshake
            });
            transport.Start();

            playerConnection.RaiseHandshake();
            acceptHandshake = false;
            tcp.RaiseHandshake();
            transport.Send("sample", 2, new object());

            Assert.That(playerConnection.SendCount, Is.EqualTo(2));
            Assert.That(tcp.SendCount, Is.EqualTo(1));
            transport.Dispose();
        }
    }
}

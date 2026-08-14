using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteEditorReconnectStateTests
    {
        [Test]
        public void PlayerConnectionAcceptsSignedPlayerIdentifiers()
        {
            var state = new RemoteEditorReconnectState
            {
                Kind = RemoteEditorConnectionKind.PlayerConnection,
                PlayerId = int.MinValue
            };

            Assert.That(state.IsValid, Is.True);
            state.PlayerId = -1;
            Assert.That(state.IsValid, Is.True);
            state.PlayerId = 42;
            Assert.That(state.IsValid, Is.True);
        }

        [Test]
        public void DirectIpRequiresHostAndValidPort()
        {
            var state = new RemoteEditorReconnectState
            {
                Kind = RemoteEditorConnectionKind.DirectTcp,
                Host = "127.0.0.1",
                Port = 56000
            };

            Assert.That(state.IsValid, Is.True);
            state.Host = string.Empty;
            Assert.That(state.IsValid, Is.False);
            state.Host = "127.0.0.1";
            state.Port = 0;
            Assert.That(state.IsValid, Is.False);
        }

        [Test]
        public void DirectIpStateRoundTripsThroughSessionJson()
        {
            var expected = new RemoteEditorReconnectState
            {
                Kind = RemoteEditorConnectionKind.DirectTcp,
                Host = "192.168.1.25",
                Port = 56000,
                AccessToken = "session-token"
            };

            string json = JsonUtility.ToJson(expected);
            RemoteEditorReconnectState actual = JsonUtility.FromJson<RemoteEditorReconnectState>(json);

            Assert.That(actual.IsValid, Is.True);
            Assert.That(actual.Kind, Is.EqualTo(RemoteEditorConnectionKind.DirectTcp));
            Assert.That(actual.Host, Is.EqualTo(expected.Host));
            Assert.That(actual.Port, Is.EqualTo(expected.Port));
            Assert.That(actual.AccessToken, Is.EqualTo(expected.AccessToken));
        }

        [Test]
        public void NegativePlayerIdentifierRoundTripsThroughSessionJson()
        {
            var expected = new RemoteEditorReconnectState
            {
                Kind = RemoteEditorConnectionKind.PlayerConnection,
                PlayerId = -135246
            };

            string json = JsonUtility.ToJson(expected);
            RemoteEditorReconnectState actual = JsonUtility.FromJson<RemoteEditorReconnectState>(json);

            Assert.That(actual.IsValid, Is.True);
            Assert.That(actual.PlayerId, Is.EqualTo(expected.PlayerId));
        }
    }
}

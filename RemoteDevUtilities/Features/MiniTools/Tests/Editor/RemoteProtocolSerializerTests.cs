using System;
using NUnit.Framework;
using SAS.DevUtilities;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Commands;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Tests
{
    public sealed class RemoteProtocolSerializerTests
    {
        [Test]
        public void MiniToolCatalog_PreservesPortableCommandManifest()
        {
            var response = new RemoteMiniToolCatalogResponse
            {
                Tools = new[]
                {
                    new RemoteMiniToolDescriptor
                    {
                        Id = "custom.network",
                        Capabilities = RemoteMiniToolCapabilities.NativeWorkspaceFields | RemoteMiniToolCapabilities.TypedDebugHostSnapshot,
                        Command = new RemoteMiniToolCommandManifest
                        {
                            Name = "Network.Show",
                            SuggestedRouting = RemoteCommandRouting.ExecuteInBuildAndControlEditorTool
                        }
                    }
                }
            };

            byte[] data = RemoteProtocolSerializer.Serialize(RemoteMiniToolMessageTypes.CatalogResponse, 19, "runtime-session", response);
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolCatalogResponse copy, out error), Is.True, error);
            Assert.That(copy.Tools[0].Command.Name, Is.EqualTo("Network.Show"));
            Assert.That(copy.Tools[0].Command.SuggestedRouting, Is.EqualTo(RemoteCommandRouting.ExecuteInBuildAndControlEditorTool));
            Assert.That(copy.Tools[0].Capabilities, Is.EqualTo(RemoteMiniToolCapabilities.NativeWorkspaceFields | RemoteMiniToolCapabilities.TypedDebugHostSnapshot));
        }

        [Test]
        public void MiniToolCatalog_PreservesActionManifest()
        {
            var response = new RemoteMiniToolCatalogResponse
            {
                Tools = new[]
                {
                    new RemoteMiniToolDescriptor
                    {
                        Id = "runtime.frame-stepper",
                        Capabilities = RemoteMiniToolCapabilities.Actions,
                        Actions = new[]
                        {
                            new RemoteMiniToolActionDescriptor
                            {
                                Id = "pause",
                                DisplayName = "Pause"
                            },
                            new RemoteMiniToolActionDescriptor
                            {
                                Id = "step",
                                DisplayName = "Step",
                                HideInNativeWorkspace = true
                            }
                        }
                    }
                }
            };

            byte[] data = RemoteProtocolSerializer.Serialize(RemoteMiniToolMessageTypes.CatalogResponse, 22, "runtime-session", response);
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolCatalogResponse copy, out error), Is.True, error);

            Assert.That(copy.Tools[0].Actions, Has.Length.EqualTo(2));
            Assert.That(copy.Tools[0].Actions[0].Id, Is.EqualTo("pause"));
            Assert.That(copy.Tools[0].Actions[1].DisplayName, Is.EqualTo("Step"));
            Assert.That(copy.Tools[0].Actions[1].HideInNativeWorkspace, Is.True);
        }

        [Test]
        public void MiniToolSample_PreservesTypedSnapshot()
        {
            var sample = new RemoteMiniToolSample
            {
                ToolId = "runtime.game-info",
                SnapshotTypeName = typeof(GameInfoSnapshot).AssemblyQualifiedName,
                SnapshotJson = "{\"GameVersion\":\"1.2.3\",\"UnityVersion\":\"6000.3\"}"
            };

            byte[] data = RemoteProtocolSerializer.Serialize(RemoteMiniToolMessageTypes.Sample, 20, "runtime-session", sample);
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolSample copy, out error), Is.True, error);
            Assert.That(copy.SnapshotTypeName, Is.EqualTo(sample.SnapshotTypeName));
            Assert.That(copy.SnapshotJson, Is.EqualTo(sample.SnapshotJson));
        }

        [Test]
        public void MiniToolStreamBatch_PreservesTypedEvents()
        {
            var payload = new RemoteMiniToolStreamPayload<TestMiniToolStreamEvent>
            {
                Events = new[]
                {
                    new TestMiniToolStreamEvent
                    {
                        Value = 42
                    }
                }
            };
            var batch = new RemoteMiniToolStreamBatch
            {
                ToolId = "tests.stream",
                Sequence = 3,
                DroppedEventCount = 2,
                EventTypeName = typeof(TestMiniToolStreamEvent).AssemblyQualifiedName,
                EventsJson = JsonUtility.ToJson(payload)
            };

            byte[] data = RemoteProtocolSerializer.Serialize(RemoteMiniToolMessageTypes.StreamBatch, 21, "runtime-session", batch);
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope, out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope, out RemoteMiniToolStreamBatch copy, out error), Is.True, error);
            Assert.That(copy.ToolId, Is.EqualTo("tests.stream"));
            Assert.That(copy.Sequence, Is.EqualTo(3));
            Assert.That(copy.DroppedEventCount, Is.EqualTo(2));

            var copiedPayload = JsonUtility.FromJson<RemoteMiniToolStreamPayload<TestMiniToolStreamEvent>>(copy.EventsJson);
            Assert.That(copiedPayload.Events, Has.Length.EqualTo(1));
            Assert.That(copiedPayload.Events[0].Value, Is.EqualTo(42));
        }

        [Test]
        public void EmptyAndOversizedMessages_AreRejected()
        {
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(Array.Empty<byte>(), out _, out string emptyError), Is.False);
            Assert.That(emptyError, Is.Not.Empty);

            byte[] oversized = new byte[RemoteProtocolConstants.MaximumMessageBytes + 1];
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(oversized, out _, out string oversizedError), Is.False);
            Assert.That(oversizedError, Does.Contain("exceeded"));
        }

        [Serializable]
        private struct TestMiniToolStreamEvent : SAS.DevUtilities.IMiniToolStreamEvent
        {
            public int Value;
        }
    }
}

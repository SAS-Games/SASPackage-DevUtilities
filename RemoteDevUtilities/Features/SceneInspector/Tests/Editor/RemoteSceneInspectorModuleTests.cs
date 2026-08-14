using System;
using System.Collections.Generic;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Commands.Presentation;
using SAS.Utilities.RemoteDevUtilities.Editor.DebugHost;
using SAS.Utilities.RemoteDevUtilities.Editor.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector;
using SAS.Utilities.RemoteDevUtilities.Protocol.Serialization;
using SAS.Utilities.RemoteDevUtilities.RuntimeSceneInspector;
using RemoteMessageTypes = SAS.Utilities.RemoteDevUtilities.Protocol.RuntimeSceneInspector.RemoteSceneInspectorMessageTypes;

namespace SAS.Utilities.RemoteDevUtilities.SceneInspector.Tests
{
    public sealed class RemoteSceneInspectorModuleTests
    {
        private sealed class RecordingSession : IRemoteEditorSession
        {
            public bool IsConnected => true;
            public readonly List<string> SentMessageTypes = new();

            public long Send<T>(string messageType, T payload)
            {
                SentMessageTypes.Add(messageType);
                return SentMessageTypes.Count;
            }

            public void NotifyStateChanged()
            {
            }
        }

        [Test]
        public void Module_ContributesEndpointClientWorkspaceCommandAndDebugHostAdapters()
        {
            IReadOnlyList<IRuntimeRemoteEndpoint> endpoints = RuntimeRemoteEndpointRegistry.CreateEndpoints();
            IReadOnlyList<IRemoteEditorFeatureClient> features = null;
            IReadOnlyList<RemoteWorkspacePanelInstance> panels = null;
            try
            {
                Assert.That(ContainsType<RemoteRuntimeSceneInspectorEndpoint>(endpoints), Is.True);

                var session = new RecordingSession();
                features = RemoteEditorFeatureRegistry.CreateFeatures(session);
                Assert.That(ContainsType<RemoteRuntimeSceneInspectorClient>(features), Is.True);
                RemoteRuntimeSceneInspectorClient client = Find<RemoteRuntimeSceneInspectorClient>(features);
                client.OnConnected();
                Assert.That(session.SentMessageTypes, Does.Contain(RemoteMessageTypes.SceneInspectorHierarchyRequest));

                panels = RemoteWorkspacePanelRegistry.CreatePanels(() => { });
                Assert.That(ContainsRegistration(panels, "runtime-scene-inspector"), Is.True);
                Assert.That(ContainsType<RemoteRuntimeSceneInspectorCommandPresentation>(
                    RemoteCommandPresentationHandlerRegistry.CreateHandlers()), Is.True);
                Assert.That(ContainsType<RemoteSceneInspectorDebugHostContribution>(
                    RemoteDebugHostContributionRegistry.CreateContributions()), Is.True);
            }
            finally
            {
                if (panels != null)
                {
                    for (int i = panels.Count - 1; i >= 0; i--)
                        panels[i].Panel.Dispose();
                }
                for (int i = endpoints.Count - 1; i >= 0; i--)
                    endpoints[i].Dispose();
            }
        }

        [Test]
        public void ProtocolPayload_PreservesNestedArrays()
        {
            var response = new RemoteSceneInspectorHierarchyResponse
            {
                Revision = 7,
                Entries = new[]
                {
                    new RemoteHierarchyEntry
                    {
                        Id = 11,
                        ParentId = 4,
                        SceneId = 1,
                        Kind = 1,
                        Name = "Player",
                        ComponentTypeNames = new[] { "Transform", "CharacterController" }
                    }
                }
            };

            byte[] data = RemoteProtocolSerializer.Serialize(RemoteMessageTypes.SceneInspectorHierarchyResponse, 8,
                "runtime-session", response);
            Assert.That(RemoteProtocolSerializer.TryDeserializeEnvelope(data, out RemoteEnvelope envelope,
                out string error), Is.True, error);
            Assert.That(RemoteProtocolSerializer.TryDeserializePayload(envelope,
                out RemoteSceneInspectorHierarchyResponse copy, out error), Is.True, error);
            Assert.That(copy.Revision, Is.EqualTo(7));
            Assert.That(copy.Entries, Has.Length.EqualTo(1));
            Assert.That(copy.Entries[0].Name, Is.EqualTo("Player"));
            Assert.That(copy.Entries[0].ComponentTypeNames,
                Is.EqualTo(new[] { "Transform", "CharacterController" }));
        }

        private static bool ContainsType<T>(System.Collections.IEnumerable values)
        {
            foreach (object value in values)
            {
                if (value is T)
                    return true;
            }
            return false;
        }

        private static T Find<T>(IEnumerable<IRemoteEditorFeatureClient> values) where T : class
        {
            foreach (IRemoteEditorFeatureClient value in values)
            {
                if (value is T match)
                    return match;
            }
            return null;
        }

        private static bool ContainsRegistration(IEnumerable<RemoteWorkspacePanelInstance> panels, string id)
        {
            foreach (RemoteWorkspacePanelInstance panel in panels)
            {
                if (string.Equals(panel.Registration.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}

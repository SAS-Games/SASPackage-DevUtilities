using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using SAS.Utilities.RemoteDevUtilities.MiniTools;
using SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools;

namespace SAS.Utilities.RemoteDevUtilities.MiniTools.Tests
{
    public sealed class RemoteMiniToolsModuleTests
    {
        [Test]
        public void ModuleRegistersRuntimeEditorAndWorkspaceSurfaces()
        {
            IReadOnlyList<IRuntimeRemoteEndpoint> endpoints = RuntimeRemoteEndpointRegistry.CreateEndpoints();
            IReadOnlyList<IRemoteEditorFeatureClient> features =
                RemoteEditorFeatureRegistry.CreateFeatures(new RecordingSession());
            IReadOnlyList<RemoteWorkspacePanelInstance> panels =
                RemoteWorkspacePanelRegistry.CreatePanels(() => { });
            try
            {
                Assert.That(endpoints.Any(endpoint => endpoint is RuntimeRemoteMiniToolEndpoint), Is.True);
                Assert.That(features.Any(feature => feature is RemoteMiniToolClient), Is.True);
                Assert.That(panels.Any(panel => panel.Registration.Id == "mini-tools"), Is.True);
            }
            finally
            {
                foreach (IRuntimeRemoteEndpoint endpoint in endpoints)
                    endpoint.Dispose();
                foreach (RemoteWorkspacePanelInstance panel in panels)
                    panel.Panel.Dispose();
            }
        }

        [Test]
        public void ConnectedLifecycleRequestsMiniToolCatalog()
        {
            var session = new RecordingSession();
            var client = new RemoteMiniToolClient(session);
            client.OnConnected();
            Assert.That(session.SentMessageTypes, Is.EqualTo(new[] { RemoteMiniToolMessageTypes.CatalogRequest }));
        }

        private sealed class RecordingSession : IRemoteEditorSession
        {
            internal readonly List<string> SentMessageTypes = new();
            public bool IsConnected => true;
            public long Send<T>(string messageType, T payload)
            {
                SentMessageTypes.Add(messageType);
                return SentMessageTypes.Count;
            }
            public void NotifyStateChanged() { }
        }
    }
}

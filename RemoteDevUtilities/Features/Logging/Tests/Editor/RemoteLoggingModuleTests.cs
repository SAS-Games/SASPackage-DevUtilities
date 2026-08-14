using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Agent;
using SAS.Utilities.RemoteDevUtilities.Editor.Client;
using SAS.Utilities.RemoteDevUtilities.Editor.Logging;
using SAS.Utilities.RemoteDevUtilities.Editor.UI;
using SAS.Utilities.RemoteDevUtilities.Logging;
using SAS.Utilities.RemoteDevUtilities.Protocol.Logging;

namespace SAS.Utilities.RemoteDevUtilities.Logging.Tests
{
    public sealed class RemoteLoggingModuleTests
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
                Assert.That(endpoints.Any(endpoint => endpoint is RuntimeRemoteLogEndpoint), Is.True);
                Assert.That(features.Any(feature => feature is RemoteLogClient), Is.True);
                Assert.That(panels.Any(panel => panel.Registration.Id == "logging"), Is.True);
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
        public void ConnectedLifecycleRequestsLoggingSettings()
        {
            var session = new RecordingSession();
            var client = new RemoteLogClient(session);
            client.OnConnected();
            Assert.That(session.SentMessageTypes, Is.EqualTo(new[] { RemoteLoggingMessageTypes.SettingsRequest }));
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

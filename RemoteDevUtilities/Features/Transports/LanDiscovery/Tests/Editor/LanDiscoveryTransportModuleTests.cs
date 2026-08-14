using System.Collections;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Transport;

namespace SAS.Utilities.RemoteDevUtilities.Transport.LanDiscovery.Tests
{
    public sealed class LanDiscoveryTransportModuleTests
    {
        [Test]
        public void Registries_DiscoverRuntimeAndEditorServices()
        {
            var runtimeServices = RuntimeRemoteConnectionServiceRegistry.CreateServices();
            var editorServices = RemoteEditorConnectionServiceRegistry.CreateServices();
            try
            {
                Assert.That(ContainsType<RuntimeLanDiscoveryBroadcaster>(runtimeServices), Is.True);
                Assert.That(ContainsType<EditorLanDiscoveryService>(editorServices), Is.True);
            }
            finally
            {
                for (int i = runtimeServices.Count - 1; i >= 0; i--)
                    runtimeServices[i].Dispose();
                for (int i = editorServices.Count - 1; i >= 0; i--)
                    editorServices[i].Dispose();
            }
        }

        private static bool ContainsType<T>(IEnumerable values)
        {
            foreach (object value in values)
            {
                if (value is T)
                    return true;
            }
            return false;
        }
    }
}

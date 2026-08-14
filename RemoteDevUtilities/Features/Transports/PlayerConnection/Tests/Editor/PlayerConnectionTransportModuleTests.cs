using System.Collections;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Transport;

namespace SAS.Utilities.RemoteDevUtilities.Transport.PlayerConnection.Tests
{
    public sealed class PlayerConnectionTransportModuleTests
    {
        [Test]
        public void Registries_DiscoverRuntimeAndEditorTransports()
        {
            IRuntimeRemoteTransport runtime = RuntimeRemoteTransportFactory.Create(
                "runtime-session", null, out var runtimeTransports);
            var editorTransports = RemoteEditorTransportRegistry.CreateTransports();
            try
            {
                Assert.That(ContainsType<RuntimePlayerConnectionTransport>(runtimeTransports), Is.True);
                Assert.That(ContainsType<EditorPlayerConnectionTransport>(editorTransports), Is.True);
            }
            finally
            {
                runtime.Dispose();
                for (int i = editorTransports.Count - 1; i >= 0; i--)
                    editorTransports[i].Dispose();
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

using System.Collections;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection.Tcp;

namespace SAS.Utilities.RemoteDevUtilities.Transport.Tcp.Tests
{
    public sealed class TcpTransportModuleTests
    {
        [Test]
        public void EditorRegistry_DiscoversTcpTransport()
        {
            var transports = RemoteEditorTransportRegistry.CreateTransports();
            try
            {
                Assert.That(ContainsType<EditorTcpConnectionTransport>(transports), Is.True);
            }
            finally
            {
                for (int i = transports.Count - 1; i >= 0; i--)
                    transports[i].Dispose();
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

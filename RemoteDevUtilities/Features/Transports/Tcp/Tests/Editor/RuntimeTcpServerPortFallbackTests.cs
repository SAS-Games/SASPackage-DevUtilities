using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Transport.Tcp;
using UnityEngine;
using UnityEngine.TestTools;

namespace SAS.Utilities.RemoteDevUtilities.Transport.Tcp.Tests
{
    public sealed class RuntimeTcpServerPortFallbackTests
    {
        [Test]
        public void Start_WhenConfiguredPortIsOccupied_BindsNextAvailablePort()
        {
            TcpListener blocker = CreateBlockerWithAvailableNextPort(out int configuredPort);
            RuntimeTcpServerTransport transport = null;
            try
            {
                transport = new RuntimeTcpServerTransport("runtime-session", IPAddress.Loopback, configuredPort, false, 1);
                LogAssert.Expect(LogType.Warning, new Regex($"Configured TCP port {configuredPort} is already in use.*selected .*:{configuredPort + 1}"));

                transport.Start();

                Assert.That(transport.IsListening, Is.True);
                Assert.That(transport.ConfiguredPort, Is.EqualTo(configuredPort));
                Assert.That(transport.BoundPort, Is.EqualTo(configuredPort + 1));
            }
            finally
            {
                transport?.Dispose();
                blocker.Stop();
            }
        }

        private static TcpListener CreateBlockerWithAvailableNextPort(out int port)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var blocker = new TcpListener(IPAddress.Loopback, 0);
                blocker.Start();
                port = ((IPEndPoint)blocker.LocalEndpoint).Port;
                if (port >= 65535)
                {
                    blocker.Stop();
                    continue;
                }

                var probe = new TcpListener(IPAddress.Loopback, port + 1);
                try
                {
                    probe.Start();
                    return blocker;
                }
                catch (SocketException)
                {
                    blocker.Stop();
                }
                finally
                {
                    probe.Stop();
                }
            }

            throw new InvalidOperationException("Could not reserve a TCP port followed by an available fallback port.");
        }
    }
}

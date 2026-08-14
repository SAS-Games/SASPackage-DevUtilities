using System.Text;
using NUnit.Framework;
using SAS.Utilities.RemoteDevUtilities.Editor.Connection;
using SAS.Utilities.RemoteDevUtilities.Protocol;
using SAS.Utilities.RemoteDevUtilities.Protocol.Connection;
using UnityEditor;
using UnityEngine;

namespace SAS.Utilities.RemoteDevUtilities.Transport.LanDiscovery.Tests
{
    public sealed class RemoteLanDiscoveryTests
    {
        [Test]
        public void Protocol_RoundTripsValidBeacon()
        {
            RemoteLanDiscoveryBeacon expected = CreateBeacon();

            byte[] data = RemoteLanDiscoveryProtocol.Serialize(expected);

            Assert.That(RemoteLanDiscoveryProtocol.TryDeserialize(data, out RemoteLanDiscoveryBeacon actual), Is.True);
            Assert.That(actual.RuntimeSessionId, Is.EqualTo(expected.RuntimeSessionId));
            Assert.That(actual.TcpPort, Is.EqualTo(expected.TcpPort));
            Assert.That(actual.Target.ProductName, Is.EqualTo(expected.Target.ProductName));
        }

        [Test]
        public void Protocol_RejectsUnrelatedUdpPayload()
        {
            byte[] data = Encoding.UTF8.GetBytes("{\"Signature\":\"another-service\"}");

            Assert.That(RemoteLanDiscoveryProtocol.TryDeserialize(data, out _), Is.False);
        }

        [Test]
        public void Registry_DeduplicatesBeaconsByRuntimeSession()
        {
            var registry = new RemoteLanDiscoveryRegistry();
            RemoteLanDiscoveryBeacon first = CreateBeacon();
            RemoteLanDiscoveryBeacon repeated = CreateBeacon();

            Assert.That(registry.Accept("192.168.1.20", first, 1d), Is.True);
            Assert.That(registry.Accept("192.168.1.20", repeated, 2d), Is.False);
            Assert.That(registry.Players, Has.Count.EqualTo(1));
            Assert.That(registry.Players[0].LastSeenTime, Is.EqualTo(2d));
        }

        [Test]
        public void Registry_UpdatesAddressAndExpiresMissingPlayer()
        {
            var registry = new RemoteLanDiscoveryRegistry();
            RemoteLanDiscoveryBeacon beacon = CreateBeacon();
            registry.Accept("192.168.1.20", beacon, 1d);

            Assert.That(registry.Accept("192.168.1.21", CreateBeacon(), 2d), Is.True);
            Assert.That(registry.Players[0].Host, Is.EqualTo("192.168.1.21"));
            Assert.That(registry.RemoveExpired(2d + RemoteLanDiscoveryConstants.EntryLifetimeSeconds + 0.01d), Is.True);
            Assert.That(registry.Players, Is.Empty);
        }

        [Test]
        public void RuntimeConfiguration_BakesNetworkSettings()
        {
            RemoteDevUtilitiesRuntimeSettings source = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            RemoteDevUtilitiesRuntimeSettings snapshot = ScriptableObject.CreateInstance<RemoteDevUtilitiesRuntimeSettings>();
            try
            {
                var serializedSource = new SerializedObject(source);
                serializedSource.FindProperty("m_EnableLanDiscovery").boolValue = false;
                serializedSource.FindProperty("m_EnableTcpPortFallback").boolValue = true;
                serializedSource.FindProperty("m_TcpPortFallbackCount").intValue = 7;
                serializedSource.ApplyModifiedPropertiesWithoutUndo();

                var configuration = new RemoteDevUtilitiesRuntimeConfiguration();
                configuration.CopyFrom(source);
                snapshot.Apply(configuration, false);

                Assert.That(configuration.EnableLanDiscovery, Is.False);
                Assert.That(snapshot.EnableLanDiscovery, Is.False);
                Assert.That(configuration.TcpPortFallbackCount, Is.EqualTo(7));
                Assert.That(snapshot.TcpPortFallbackCount, Is.EqualTo(7));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(snapshot);
            }
        }

        private static RemoteLanDiscoveryBeacon CreateBeacon()
        {
            return new RemoteLanDiscoveryBeacon
            {
                Signature = RemoteLanDiscoveryConstants.Signature,
                ProtocolVersion = RemoteProtocolConstants.Version,
                PackageVersion = RemoteProtocolConstants.PackageVersion,
                RuntimeSessionId = "runtime-session",
                TcpPort = RemoteProtocolConstants.DefaultTcpPort,
                Target = new RemoteTargetDescriptor
                {
                    ProductName = "Sample Game",
                    ApplicationVersion = "1.0",
                    UnityVersion = "2022.3",
                    Platform = "WindowsPlayer",
                    DeviceName = "Test Device",
                    IsDebugBuild = true,
                    IsDevUtilitiesEnabled = true
                }
            };
        }
    }
}

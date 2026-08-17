using System;
using System.Text;
using HP.Utilities.RemoteDevUtilities.Protocol.Connection;
using UnityEngine;

namespace HP.Utilities.RemoteDevUtilities.Protocol
{
    [Serializable]
    internal sealed class RemoteLanDiscoveryBeacon
    {
        public string Signature;
        public int ProtocolVersion;
        public string PackageVersion;
        public string RuntimeSessionId;
        public int TcpPort;
        public RemoteTargetDescriptor Target;
    }

    internal static class RemoteLanDiscoveryProtocol
    {
        private const int MaximumBeaconBytes = 16 * 1024;

        public static byte[] Serialize(RemoteLanDiscoveryBeacon beacon)
        {
            if (beacon == null)
                throw new ArgumentNullException(nameof(beacon));

            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(beacon));
        }

        public static bool TryDeserialize(byte[] data, out RemoteLanDiscoveryBeacon beacon)
        {
            beacon = null;
            if (data == null || data.Length == 0 || data.Length > MaximumBeaconBytes)
                return false;

            try
            {
                beacon = JsonUtility.FromJson<RemoteLanDiscoveryBeacon>(Encoding.UTF8.GetString(data));
            }
            catch (ArgumentException)
            {
                beacon = null;
                return false;
            }

            if (beacon == null ||
                !string.Equals(beacon.Signature, RemoteLanDiscoveryConstants.Signature, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(beacon.RuntimeSessionId) ||
                beacon.TcpPort < 1 || beacon.TcpPort > 65535 ||
                beacon.Target == null || !beacon.Target.IsDevUtilitiesEnabled)
            {
                beacon = null;
                return false;
            }

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using SAS.Utilities.RemoteDevUtilities.Protocol;

namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    internal sealed class RemoteLanDiscoveryRegistry
    {
        private readonly Dictionary<string, RemoteLanPlayerDescriptor> _playersBySession = new(StringComparer.Ordinal);
        private readonly List<RemoteLanPlayerDescriptor> _players = new();

        public IReadOnlyList<RemoteLanPlayerDescriptor> Players => _players;

        public bool Accept(string host, RemoteLanDiscoveryBeacon beacon, double now)
        {
            if (string.IsNullOrWhiteSpace(host) || beacon == null || string.IsNullOrWhiteSpace(beacon.RuntimeSessionId))
                return false;

            bool changed;
            if (!_playersBySession.TryGetValue(beacon.RuntimeSessionId, out RemoteLanPlayerDescriptor player))
            {
                player = new RemoteLanPlayerDescriptor { RuntimeSessionId = beacon.RuntimeSessionId };
                _playersBySession.Add(beacon.RuntimeSessionId, player);
                _players.Add(player);
                changed = true;
            }
            else
            {
                changed = !HasSameEndpointAndMetadata(player, host, beacon);
            }

            player.Host = host;
            player.Port = beacon.TcpPort;
            player.ProtocolVersion = beacon.ProtocolVersion;
            player.PackageVersion = beacon.PackageVersion;
            player.Target = beacon.Target;
            player.LastSeenTime = now;

            if (changed)
                SortPlayers();
            return changed;
        }

        public bool RemoveExpired(double now, double lifetimeSeconds = RemoteProtocolConstants.LanDiscoveryEntryLifetimeSeconds)
        {
            bool changed = false;
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                RemoteLanPlayerDescriptor player = _players[i];
                if (now - player.LastSeenTime <= lifetimeSeconds)
                    continue;

                _players.RemoveAt(i);
                _playersBySession.Remove(player.RuntimeSessionId);
                changed = true;
            }

            return changed;
        }

        public bool Clear()
        {
            if (_players.Count == 0)
                return false;

            _players.Clear();
            _playersBySession.Clear();
            return true;
        }

        private static bool HasSameEndpointAndMetadata(RemoteLanPlayerDescriptor player, string host, RemoteLanDiscoveryBeacon beacon)
        {
            if (!string.Equals(player.Host, host, StringComparison.Ordinal) || player.Port != beacon.TcpPort ||
                player.ProtocolVersion != beacon.ProtocolVersion || !string.Equals(player.PackageVersion, beacon.PackageVersion, StringComparison.Ordinal))
                return false;

            if (ReferenceEquals(player.Target, beacon.Target))
                return true;
            if (player.Target == null || beacon.Target == null)
                return false;

            return string.Equals(player.Target.ProductName, beacon.Target.ProductName, StringComparison.Ordinal) &&
                   string.Equals(player.Target.ApplicationVersion, beacon.Target.ApplicationVersion, StringComparison.Ordinal) &&
                   string.Equals(player.Target.UnityVersion, beacon.Target.UnityVersion, StringComparison.Ordinal) &&
                   string.Equals(player.Target.Platform, beacon.Target.Platform, StringComparison.Ordinal) &&
                   string.Equals(player.Target.DeviceName, beacon.Target.DeviceName, StringComparison.Ordinal) &&
                   player.Target.IsDebugBuild == beacon.Target.IsDebugBuild &&
                   player.Target.IsDevUtilitiesEnabled == beacon.Target.IsDevUtilitiesEnabled;
        }

        private void SortPlayers()
        {
            _players.Sort((left, right) =>
            {
                int product = string.Compare(left.Target?.ProductName, right.Target?.ProductName, StringComparison.OrdinalIgnoreCase);
                if (product != 0)
                    return product;
                int device = string.Compare(left.Target?.DeviceName, right.Target?.DeviceName, StringComparison.OrdinalIgnoreCase);
                return device != 0 ? device : string.Compare(left.Host, right.Host, StringComparison.OrdinalIgnoreCase);
            });
        }
    }
}

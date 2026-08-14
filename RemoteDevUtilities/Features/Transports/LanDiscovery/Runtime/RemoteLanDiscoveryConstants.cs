namespace SAS.Utilities.RemoteDevUtilities.Protocol
{
    public static class RemoteLanDiscoveryConstants
    {
        public const int Port = 56001;
        public const string Signature = "SAS.RemoteDevUtilities.Discovery.v1";
        public const float BeaconIntervalSeconds = 3f;
        public const double EntryLifetimeSeconds = 10d;
    }
}

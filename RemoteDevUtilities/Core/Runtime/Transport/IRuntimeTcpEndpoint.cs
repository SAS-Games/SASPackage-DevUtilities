namespace SAS.Utilities.RemoteDevUtilities.Transport
{
    internal interface IRuntimeTcpEndpoint
    {
        bool IsListening { get; }
        int ConfiguredPort { get; }
        int BoundPort { get; }
    }
}

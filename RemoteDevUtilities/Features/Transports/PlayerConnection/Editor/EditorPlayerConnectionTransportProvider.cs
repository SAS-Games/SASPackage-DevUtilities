namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    [RemoteEditorTransportProvider(RemoteEditorTransportIds.PlayerConnection, 100)]
    internal sealed class EditorPlayerConnectionTransportProvider : IRemoteEditorTransportProvider
    {
        public IRemoteEditorTransport Create() => new EditorPlayerConnectionTransport();
    }
}

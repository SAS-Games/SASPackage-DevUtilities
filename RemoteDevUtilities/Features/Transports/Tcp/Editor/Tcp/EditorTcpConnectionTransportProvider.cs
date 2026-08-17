namespace HP.Utilities.RemoteDevUtilities.Editor.Connection.Tcp
{
    [RemoteEditorTransportProvider(RemoteEditorTransportIds.Tcp, 200)]
    internal sealed class EditorTcpConnectionTransportProvider : IRemoteEditorTransportProvider
    {
        public IRemoteEditorTransport Create() => new EditorTcpConnectionTransport();
    }
}

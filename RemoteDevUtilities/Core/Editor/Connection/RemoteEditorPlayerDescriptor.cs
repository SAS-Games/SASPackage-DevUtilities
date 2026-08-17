namespace SAS.Utilities.RemoteDevUtilities.Editor.Connection
{
    internal readonly struct RemoteEditorPlayerDescriptor
    {
        public RemoteEditorPlayerDescriptor(int playerId, string name)
        {
            PlayerId = playerId;
            Name = name;
        }

        public int PlayerId { get; }
        public string Name { get; }
    }
}

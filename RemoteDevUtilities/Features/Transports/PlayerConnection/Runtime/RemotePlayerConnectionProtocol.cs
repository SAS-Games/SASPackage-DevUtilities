using System;

namespace HP.Utilities.RemoteDevUtilities.Protocol
{
    public static class RemotePlayerConnectionProtocol
    {
        public static readonly Guid EditorToPlayerMessageId = new("e993cd15-4701-46fc-996b-cba576c04774");
        public static readonly Guid PlayerToEditorMessageId = new("1a511547-fd46-4c48-8b28-e9d60f45d964");
    }
}

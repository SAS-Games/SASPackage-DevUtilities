using System;

namespace SAS.Utilities.RemoteDevUtilities.Protocol
{
    public static class RemoteProtocolConstants
    {
        public const int Version = 1;
        public const string PackageVersion = "1.5.0";
        public const int MaximumMessageBytes = 8 * 1024 * 1024;
        public const int DefaultTcpPort = 56000;

        public static readonly Guid EditorToPlayerMessageId = new("e993cd15-4701-46fc-996b-cba576c04774");
        public static readonly Guid PlayerToEditorMessageId = new("1a511547-fd46-4c48-8b28-e9d60f45d964");
    }

    public static class RemoteMessageTypes
    {
        public const string HandshakeRequest = "connection.handshake.request";
        public const string HandshakeResponse = "connection.handshake.response";
        public const string SessionEndRequest = "connection.session-end.request";
        public const string SessionEndResponse = "connection.session-end.response";
        public const string PingRequest = "connection.ping.request";
        public const string PingResponse = "connection.ping.response";

        public const string CommandCatalogRequest = "commands.catalog.request";
        public const string CommandCatalogResponse = "commands.catalog.response";
        public const string CommandExecuteRequest = "commands.execute.request";
        public const string CommandExecuteResponse = "commands.execute.response";

        public const string LogBatch = "logging.batch";
        public const string LogSettingsRequest = "logging.settings.request";
        public const string LogSettingsResponse = "logging.settings.response";

        public const string MiniToolCatalogRequest = "minitools.catalog.request";
        public const string MiniToolCatalogResponse = "minitools.catalog.response";
        public const string MiniToolSubscriptionRequest = "minitools.subscription.request";
        public const string MiniToolSubscriptionResponse = "minitools.subscription.response";
        public const string MiniToolActionRequest = "minitools.action.request";
        public const string MiniToolActionResponse = "minitools.action.response";
        public const string MiniToolSample = "minitools.sample";
        public const string MiniToolStreamBatch = "minitools.stream.batch";

        public const string SceneInspectorHierarchyRequest = "scene-inspector.hierarchy.request";
        public const string SceneInspectorHierarchyResponse = "scene-inspector.hierarchy.response";
        public const string SceneInspectorInspectRequest = "scene-inspector.inspect.request";
        public const string SceneInspectorInspectResponse = "scene-inspector.inspect.response";
        public const string SceneInspectorCommandRequest = "scene-inspector.command.request";
        public const string SceneInspectorCommandResponse = "scene-inspector.command.response";
    }
}

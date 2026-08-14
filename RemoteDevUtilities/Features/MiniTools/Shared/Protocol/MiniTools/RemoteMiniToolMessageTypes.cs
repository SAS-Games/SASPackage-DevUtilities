namespace SAS.Utilities.RemoteDevUtilities.Protocol.MiniTools
{
    public static class RemoteMiniToolMessageTypes
    {
        public const string CatalogRequest = "minitools.catalog.request";
        public const string CatalogResponse = "minitools.catalog.response";
        public const string SubscriptionRequest = "minitools.subscription.request";
        public const string SubscriptionResponse = "minitools.subscription.response";
        public const string ActionRequest = "minitools.action.request";
        public const string ActionResponse = "minitools.action.response";
        public const string Sample = "minitools.sample";
        public const string StreamBatch = "minitools.stream.batch";
    }
}

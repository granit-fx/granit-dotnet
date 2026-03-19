namespace Granit.Webhooks.Endpoints.Permissions;

/// <summary>
/// Permission constants for webhook administration.
/// </summary>
public static class WebhooksPermissions
{
    public const string GroupName = "Webhooks";

    public static class Subscriptions
    {
        public const string View = "Webhooks.Subscriptions.View";
        public const string Create = "Webhooks.Subscriptions.Create";
        public const string Update = "Webhooks.Subscriptions.Update";
        public const string Delete = "Webhooks.Subscriptions.Delete";
        public const string Manage = "Webhooks.Subscriptions.Manage";
    }
}

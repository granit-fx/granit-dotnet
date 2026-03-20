namespace Granit.Webhooks.Endpoints.Permissions;

/// <summary>
/// Permission constants for webhook administration.
/// </summary>
public static class WebhooksPermissions
{
    public const string GroupName = "Webhooks";

    /// <summary>Permissions for the webhook subscriptions resource.</summary>
    public static class Subscriptions
    {
        /// <summary>Grants read-only access to view webhook subscriptions.</summary>
        public const string Read = "Webhooks.Subscriptions.Read";

        /// <summary>Grants management access to webhook subscriptions (create, update, delete).</summary>
        public const string Manage = "Webhooks.Subscriptions.Manage";
    }
}

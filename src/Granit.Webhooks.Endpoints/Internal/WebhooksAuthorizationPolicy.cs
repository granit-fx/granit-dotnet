using Granit.Webhooks.Endpoints.Permissions;

namespace Granit.Webhooks.Endpoints.Internal;

internal static class WebhooksAuthorizationPolicy
{
    internal const string PolicyName = WebhooksPermissions.Subscriptions.Manage;
}

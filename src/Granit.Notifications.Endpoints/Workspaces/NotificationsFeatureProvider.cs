using Granit.Notifications.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Notifications.Endpoints.Workspaces;

/// <summary>Declares the notifications module's features (per ADR-057).</summary>
internal sealed class NotificationsFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(NotificationsFeatures.User, f => f
            .Permission(NotificationPermissions.UserNotifications.Read)
            .RouteName(NotificationsFeatures.User)
            .DefaultIcon("bell")
            .DisplayKey("NotificationsEndpoints:Workspace.Item"));
}

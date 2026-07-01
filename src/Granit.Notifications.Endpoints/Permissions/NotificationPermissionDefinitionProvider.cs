using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Notifications.Endpoints.Internal;

namespace Granit.Notifications.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Notifications.UserNotifications.Read</c> and <c>Notifications.UserNotifications.Manage</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <see cref="GranitNotificationsEndpointsModule"/>.
/// Once registered, <c>DynamicPermissionPolicyProvider</c> creates the authorization policy
/// via <c>PermissionRequirement</c> — the full <c>IPermissionChecker</c> pipeline is used:
/// </para>
/// <list type="number">
/// <item><c>AlwaysAllow</c> (dev/test, <c>GranitAuthorizationOptions.AlwaysAllow = true</c>)</item>
/// <item>AdminRole bypass (<c>GranitAuthorizationOptions.AdminRoles</c>)</item>
/// <item>Cache + <c>IPermissionGrantStore</c> query per role</item>
/// </list>
/// <para>
/// In production, grant the permission to the desired Keycloak role via one of:
/// <list type="bullet">
/// <item>Add the role to <c>GranitAuthorizationOptions.AdminRoles</c> in <c>appsettings.json</c></item>
/// <item>Call <c>IPermissionManagerWriter.SetAsync("Notifications.UserNotifications.Read", "my-role", tenantId, true)</c></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class NotificationPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            NotificationPermissions.GroupName,
            LocalizableString.Create<NotificationsEndpointsLocalizationResource>(
                "PermissionGroup:Notifications"));

        group.AddPermission(
            NotificationPermissions.UserNotifications.Read,
            LocalizableString.Create<NotificationsEndpointsLocalizationResource>(
                "Permission:Notifications.UserNotifications.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            NotificationPermissions.UserNotifications.Manage,
            LocalizableString.Create<NotificationsEndpointsLocalizationResource>(
                "Permission:Notifications.UserNotifications.Manage"),
            MultiTenancySides.Both);
    }
}

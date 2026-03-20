using Granit.Authorization.Abstractions;
using Granit.Core.Localization;
using Granit.Notifications.Endpoints.Internal;

namespace Granit.Notifications.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Notifications.Notifications.Read</c> and <c>Notifications.Notifications.Manage</c> permissions in the Granit RBAC system.
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
/// <item>Call <c>IPermissionManagerWriter.SetAsync("Notifications.Read", "my-role", tenantId, true)</c></item>
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
            NotificationPermissions.Notifications.Read,
            LocalizableString.Create<NotificationsEndpointsLocalizationResource>(
                "Permission:Notifications.Notifications.Read"));

        group.AddPermission(
            NotificationPermissions.Notifications.Manage,
            LocalizableString.Create<NotificationsEndpointsLocalizationResource>(
                "Permission:Notifications.Notifications.Manage"));
    }
}

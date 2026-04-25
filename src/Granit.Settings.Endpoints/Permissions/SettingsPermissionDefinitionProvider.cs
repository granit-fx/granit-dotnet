using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Settings.Endpoints.Internal;

namespace Granit.Settings.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for settings administration endpoints.
/// </summary>
internal sealed class SettingsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            SettingsPermissions.GroupName,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "PermissionGroup:Settings"));

        // Global settings live at the host level by design — the permission name itself
        // advertises the scope.
        group.AddPermission(
            SettingsPermissions.Global.Read,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Global.Read"),
            MultiTenancySides.Host);

        group.AddPermission(
            SettingsPermissions.Global.Manage,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Global.Manage"),
            MultiTenancySides.Host);

        // Tenant settings are only meaningful inside a tenant context.
        group.AddPermission(
            SettingsPermissions.Tenant.Read,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Tenant.Read"),
            MultiTenancySides.Tenant);

        group.AddPermission(
            SettingsPermissions.Tenant.Manage,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Tenant.Manage"),
            MultiTenancySides.Tenant);
    }
}

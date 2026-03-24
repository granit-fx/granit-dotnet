using Granit.Authorization.Abstractions;
using Granit.Localization;
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

        group.AddPermission(
            SettingsPermissions.Global.Read,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Global.Read"));

        group.AddPermission(
            SettingsPermissions.Global.Manage,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Global.Manage"));

        group.AddPermission(
            SettingsPermissions.Tenant.Read,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Tenant.Read"));

        group.AddPermission(
            SettingsPermissions.Tenant.Manage,
            LocalizableString.Create<SettingsEndpointsLocalizationResource>(
                "Permission:Settings.Tenant.Manage"));
    }
}

using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy.Endpoints.Internal;

namespace Granit.MultiTenancy.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for multi-tenancy management endpoints.
/// </summary>
internal sealed class MultiTenancyPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc/>
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            MultiTenancyPermissions.GroupName,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "PermissionGroup:MultiTenancy"));

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Read,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Read"));

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Create,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Create"));

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Update,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Update"));

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Manage,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Manage"));
    }
}

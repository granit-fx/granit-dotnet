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

        // Managing tenants is inherently cross-tenant and can only be authorized in the host context.
        group.AddPermission(
            MultiTenancyPermissions.Tenants.Read,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Read"),
            MultiTenancySides.Host);

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Create,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Create"),
            MultiTenancySides.Host);

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Update,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Update"),
            MultiTenancySides.Host);

        group.AddPermission(
            MultiTenancyPermissions.Tenants.Manage,
            LocalizableString.Create<MultiTenancyEndpointsLocalizationResource>(
                "Permission:MultiTenancy.Tenants.Manage"),
            MultiTenancySides.Host);
    }
}

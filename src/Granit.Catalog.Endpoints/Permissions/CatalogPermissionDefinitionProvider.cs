using Granit.Authorization;
using Granit.Catalog.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.Catalog.Endpoints.Permissions;

internal sealed class CatalogPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            CatalogPermissions.GroupName,
            LocalizableString.Create<CatalogEndpointsLocalizationResource>(
                "PermissionGroup:Catalog"));

        // MVP scope: catalog is Host-owned. Both Read and Manage are Host-side only.
        // When the e-commerce phase introduces tenant-scoped products (ADR 032),
        // a Both-sided variant or a separate Tenant.* permission set will be added.
        group.AddPermission(CatalogPermissions.Products.Read,
            LocalizableString.Create<CatalogEndpointsLocalizationResource>("Permission:Catalog.Products.Read"),
            MultiTenancySides.Host);
        group.AddPermission(CatalogPermissions.Products.Manage,
            LocalizableString.Create<CatalogEndpointsLocalizationResource>("Permission:Catalog.Products.Manage"),
            MultiTenancySides.Host);
    }
}

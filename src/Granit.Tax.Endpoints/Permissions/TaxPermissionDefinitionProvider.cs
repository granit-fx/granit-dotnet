using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Tax.Endpoints.Internal;

namespace Granit.Tax.Endpoints.Permissions;

internal sealed class TaxPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            TaxPermissions.GroupName,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("PermissionGroup:Tax"));

        // Both-sided: tax rules apply to SaaS invoicing of tenants (host) AND to the
        // tenant's own billing flow (tenant). Validations run from either context.
        group.AddPermission(TaxPermissions.Rates.Read,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Rates.Read"),
            MultiTenancySides.Both);
        group.AddPermission(TaxPermissions.Rates.Manage,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Rates.Manage"),
            MultiTenancySides.Both);
        group.AddPermission(TaxPermissions.Validations.Read,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Validations.Read"),
            MultiTenancySides.Both);
        group.AddPermission(TaxPermissions.Validations.Execute,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Validations.Execute"),
            MultiTenancySides.Both);
    }
}

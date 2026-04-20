using Granit.Authorization;
using Granit.Localization;
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
            MultiTenancySide.Both);
        group.AddPermission(TaxPermissions.Rates.Manage,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Rates.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(TaxPermissions.Validations.Read,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Validations.Read"),
            MultiTenancySide.Both);
        group.AddPermission(TaxPermissions.Validations.Execute,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Validations.Execute"),
            MultiTenancySide.Both);
    }
}

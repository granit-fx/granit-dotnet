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

        group.AddPermission(TaxPermissions.Rates.Read,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Rates.Read"));
        group.AddPermission(TaxPermissions.Rates.Manage,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Rates.Manage"));
        group.AddPermission(TaxPermissions.Validations.Read,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Validations.Read"));
        group.AddPermission(TaxPermissions.Validations.Execute,
            LocalizableString.Create<TaxEndpointsLocalizationResource>("Permission:Tax.Validations.Execute"));
    }
}

using Granit.Authorization;
using Granit.CustomerBalance.Endpoints.Internal;
using Granit.Localization;

namespace Granit.CustomerBalance.Endpoints.Permissions;

internal sealed class CustomerBalancePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            CustomerBalancePermissions.GroupName,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "PermissionGroup:CustomerBalance"));

        group.AddPermission(CustomerBalancePermissions.Accounts.Read,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "Permission:CustomerBalance.Accounts.Read"));
        group.AddPermission(CustomerBalancePermissions.Transactions.Read,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "Permission:CustomerBalance.Transactions.Read"));
        group.AddPermission(CustomerBalancePermissions.Credits.Manage,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "Permission:CustomerBalance.Credits.Manage"));
    }
}

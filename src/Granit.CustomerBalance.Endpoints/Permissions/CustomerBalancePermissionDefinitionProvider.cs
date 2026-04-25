using Granit.Authorization;
using Granit.CustomerBalance.Endpoints.Internal;
using Granit.Localization;
using Granit.MultiTenancy;

namespace Granit.CustomerBalance.Endpoints.Permissions;

internal sealed class CustomerBalancePermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            CustomerBalancePermissions.GroupName,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "PermissionGroup:CustomerBalance"));

        // Both-sided: the host tracks balances of its tenants (credit issuance, write-offs)
        // while tenants see their own balance and transactions. Consumers whose tenants bill
        // their own customers can tighten these to Tenant in their own provider.
        group.AddPermission(CustomerBalancePermissions.Accounts.Read,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "Permission:CustomerBalance.Accounts.Read"),
            MultiTenancySides.Both);
        group.AddPermission(CustomerBalancePermissions.Transactions.Read,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "Permission:CustomerBalance.Transactions.Read"),
            MultiTenancySides.Both);
        group.AddPermission(CustomerBalancePermissions.Credits.Manage,
            LocalizableString.Create<CustomerBalanceEndpointsLocalizationResource>(
                "Permission:CustomerBalance.Credits.Manage"),
            MultiTenancySides.Both);
    }
}

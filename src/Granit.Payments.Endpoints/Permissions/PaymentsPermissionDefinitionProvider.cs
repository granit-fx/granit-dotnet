using Granit.Authorization;
using Granit.Localization;
using Granit.Payments.Endpoints.Internal;

namespace Granit.Payments.Endpoints.Permissions;

internal sealed class PaymentsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            PaymentsPermissions.GroupName,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("PermissionGroup:Payments"));

        // Both-sided: the host executes charges and refunds on behalf of tenants (SaaS
        // collection flow) while tenants must be able to inspect their own transactions
        // and manage their stored payment methods. Consumers running a tenant-only
        // merchant flow can tighten these to Tenant in their own provider.
        group.AddPermission(PaymentsPermissions.Transactions.Read,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Transactions.Read"),
            MultiTenancySide.Both);
        group.AddPermission(PaymentsPermissions.Charges.Execute,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Charges.Execute"),
            MultiTenancySide.Both);
        group.AddPermission(PaymentsPermissions.Refunds.Execute,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Refunds.Execute"),
            MultiTenancySide.Both);
        group.AddPermission(PaymentsPermissions.Methods.Read,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Methods.Read"),
            MultiTenancySide.Both);
        group.AddPermission(PaymentsPermissions.Methods.Manage,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Methods.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(PaymentsPermissions.Configuration.Manage,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Configuration.Manage"),
            MultiTenancySide.Both);
    }
}

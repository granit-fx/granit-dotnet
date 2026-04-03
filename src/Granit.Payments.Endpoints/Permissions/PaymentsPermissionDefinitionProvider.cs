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

        group.AddPermission(PaymentsPermissions.Transactions.Read,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Transactions.Read"));
        group.AddPermission(PaymentsPermissions.Charges.Execute,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Charges.Execute"));
        group.AddPermission(PaymentsPermissions.Refunds.Execute,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Refunds.Execute"));
        group.AddPermission(PaymentsPermissions.Methods.Read,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Methods.Read"));
        group.AddPermission(PaymentsPermissions.Methods.Manage,
            LocalizableString.Create<PaymentsEndpointsLocalizationResource>("Permission:Payments.Methods.Manage"));
    }
}

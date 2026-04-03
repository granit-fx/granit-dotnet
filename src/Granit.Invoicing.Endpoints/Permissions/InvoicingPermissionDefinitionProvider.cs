using Granit.Authorization;
using Granit.Invoicing.Endpoints.Internal;
using Granit.Localization;

namespace Granit.Invoicing.Endpoints.Permissions;

internal sealed class InvoicingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            InvoicingPermissions.GroupName,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("PermissionGroup:Invoicing"));

        group.AddPermission(InvoicingPermissions.Invoices.Read,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.Invoices.Read"));
        group.AddPermission(InvoicingPermissions.Invoices.Manage,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.Invoices.Manage"));
        group.AddPermission(InvoicingPermissions.Invoices.Download,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.Invoices.Download"));
        group.AddPermission(InvoicingPermissions.CreditNotes.Read,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.CreditNotes.Read"));
        group.AddPermission(InvoicingPermissions.CreditNotes.Manage,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.CreditNotes.Manage"));
    }
}

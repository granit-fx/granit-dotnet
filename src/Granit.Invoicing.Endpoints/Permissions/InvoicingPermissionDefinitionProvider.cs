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

        // Both-sided: in a SaaS, the host issues invoices to its tenants (host side), and
        // each tenant consults/downloads those invoices from inside its own scope (tenant
        // side). Consumers who run a pure B2B flow — a tenant billing its own customers —
        // can tighten these to Tenant-only in their own permission provider.
        group.AddPermission(InvoicingPermissions.Invoices.Read,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.Invoices.Read"),
            MultiTenancySide.Both);
        group.AddPermission(InvoicingPermissions.Invoices.Manage,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.Invoices.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(InvoicingPermissions.Invoices.Download,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.Invoices.Download"),
            MultiTenancySide.Both);
        group.AddPermission(InvoicingPermissions.CreditNotes.Read,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.CreditNotes.Read"),
            MultiTenancySide.Both);
        group.AddPermission(InvoicingPermissions.CreditNotes.Manage,
            LocalizableString.Create<InvoicingEndpointsLocalizationResource>("Permission:Invoicing.CreditNotes.Manage"),
            MultiTenancySide.Both);
    }
}

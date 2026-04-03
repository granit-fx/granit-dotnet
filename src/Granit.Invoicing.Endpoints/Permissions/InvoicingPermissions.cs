namespace Granit.Invoicing.Endpoints.Permissions;

/// <summary>Permission constants for Granit.Invoicing.Endpoints.</summary>
public static class InvoicingPermissions
{
    public const string GroupName = "Invoicing";

    public static class Invoices
    {
        public const string Read = "Invoicing.Invoices.Read";
        public const string Manage = "Invoicing.Invoices.Manage";
        public const string Download = "Invoicing.Invoices.Download";
    }

    public static class CreditNotes
    {
        public const string Read = "Invoicing.CreditNotes.Read";
        public const string Manage = "Invoicing.CreditNotes.Manage";
    }
}

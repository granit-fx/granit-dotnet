namespace Granit.Invoicing.Domain;

/// <summary>Type of financial document.</summary>
public enum InvoiceDocumentType
{
    /// <summary>Standard invoice — customer owes money (positive amount).</summary>
    Invoice = 0,

    /// <summary>Credit note — adjustment on a previous invoice (negative amount, linked via ParentInvoiceId).</summary>
    CreditNote = 1,
}

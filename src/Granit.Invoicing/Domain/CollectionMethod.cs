namespace Granit.Invoicing.Domain;

/// <summary>How payment is collected for an invoice.</summary>
public enum CollectionMethod
{
    /// <summary>Automatically charge the customer's saved payment method.</summary>
    Auto = 0,

    /// <summary>Send the invoice to the customer for manual payment.</summary>
    SendInvoice = 1,
}

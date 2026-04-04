namespace Granit.Invoicing.Domain;

/// <summary>Origin of an invoice line item. Enables agnosticism across modules.</summary>
public enum InvoiceSourceType
{
    /// <summary>Line item from a subscription billing cycle.</summary>
    Subscription = 0,

    /// <summary>Line item from usage-based metering.</summary>
    Usage = 1,

    /// <summary>One-shot purchase (e-commerce, add-on).</summary>
    OneShot = 2,

    /// <summary>Credit (negative amount — promotional credit, adjustment).</summary>
    Credit = 3,
}

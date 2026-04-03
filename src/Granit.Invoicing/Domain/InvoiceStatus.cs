namespace Granit.Invoicing.Domain;

/// <summary>Invoice lifecycle status, managed by <c>Granit.Workflow</c> FSM.</summary>
public enum InvoiceStatus
{
    /// <summary>Mutable — line items, tax, address can be modified.</summary>
    Draft = 0,

    /// <summary>Finalized, awaiting payment. Financial fields are immutable.</summary>
    Open = 1,

    /// <summary>Payment received. Terminal state.</summary>
    Paid = 2,

    /// <summary>Cancelled. Maintains paper trail and invoice number. Terminal state.</summary>
    Void = 3,

    /// <summary>Bad debt write-off. Can still transition to Paid (recovery) or Void.</summary>
    Uncollectible = 4,
}

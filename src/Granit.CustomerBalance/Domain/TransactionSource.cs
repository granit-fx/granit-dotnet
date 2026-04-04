namespace Granit.CustomerBalance.Domain;

/// <summary>
/// Origin of a balance transaction.
/// </summary>
public enum TransactionSource
{
    /// <summary>Promotional credit (e.g., "200 EUR free for new signups").</summary>
    Promotional = 0,

    /// <summary>Surplus from an overpaid invoice.</summary>
    Overpayment = 1,

    /// <summary>Administrative adjustment (manual credit or debit).</summary>
    ManualAdjustment = 2,

    /// <summary>Deduction applied to an invoice before PSP charge.</summary>
    InvoiceDeduction = 3,

    /// <summary>Credit from a refund.</summary>
    RefundCredit = 4,

    /// <summary>Debit from an expired promotional credit.</summary>
    Expiration = 5,
}

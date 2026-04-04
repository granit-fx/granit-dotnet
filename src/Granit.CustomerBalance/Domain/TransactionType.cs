namespace Granit.CustomerBalance.Domain;

/// <summary>
/// Direction of a balance transaction.
/// </summary>
public enum TransactionType
{
    /// <summary>Funds added to the balance (increases available credit).</summary>
    Credit = 0,

    /// <summary>Funds consumed from the balance (decreases available credit).</summary>
    Debit = 1,
}

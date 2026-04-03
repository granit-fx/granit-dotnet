namespace Granit.Payments.SepaDirectDebit.Domain;

/// <summary>SEPA Direct Debit scheme.</summary>
public enum SddScheme
{
    /// <summary>SEPA Core scheme — B2C and B2B. 8-week unconditional refund right.</summary>
    Core = 0,

    /// <summary>SEPA B2B scheme — B2B only. No refund right (bank-verified mandate).</summary>
    B2B = 1,
}

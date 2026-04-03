namespace Granit.Payments.Domain;

/// <summary>Type of payment method.</summary>
public enum PaymentMethodType
{
    /// <summary>Credit/debit card.</summary>
    Card = 0,

    /// <summary>SEPA Direct Debit mandate.</summary>
    SepaDebit = 1,

    /// <summary>Bank transfer (SEPA Credit Transfer).</summary>
    BankTransfer = 2,

    /// <summary>iDEAL (Netherlands).</summary>
    Ideal = 3,

    /// <summary>Bancontact (Belgium).</summary>
    Bancontact = 4,
}

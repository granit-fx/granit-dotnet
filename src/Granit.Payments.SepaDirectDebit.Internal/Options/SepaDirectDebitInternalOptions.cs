using System.ComponentModel.DataAnnotations;

namespace Granit.Payments.SepaDirectDebit.Internal.Options;

/// <summary>Configuration for the self-hosted SEPA Direct Debit provider.</summary>
public sealed class SepaDirectDebitInternalOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:SepaDirectDebit:Internal";

    /// <summary>Creditor IBAN (merchant bank account).</summary>
    [Required]
    public string CreditorIban { get; set; } = string.Empty;

    /// <summary>Creditor BIC/SWIFT.</summary>
    [Required]
    public string CreditorBic { get; set; } = string.Empty;

    /// <summary>Creditor name (as shown in PAIN.008).</summary>
    [Required]
    public string CreditorName { get; set; } = string.Empty;

    /// <summary>SEPA Creditor Identifier (e.g., BE68ZZZ0123456789).</summary>
    [Required]
    public string CreditorId { get; set; } = string.Empty;

    /// <summary>Default SDD scheme.</summary>
    public Domain.SddScheme DefaultScheme { get; set; } = Domain.SddScheme.Core;
}

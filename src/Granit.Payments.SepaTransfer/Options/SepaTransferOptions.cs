using System.ComponentModel.DataAnnotations;

namespace Granit.Payments.SepaTransfer.Options;

/// <summary>Configuration for the SEPA bank transfer payment provider.</summary>
public sealed class SepaTransferOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Payments:SepaTransfer";

    /// <summary>Beneficiary IBAN (the merchant's bank account).</summary>
    [Required]
    public string Iban { get; set; } = string.Empty;

    /// <summary>Beneficiary BIC/SWIFT code.</summary>
    [Required]
    public string Bic { get; set; } = string.Empty;

    /// <summary>Beneficiary name (as shown on bank transfer instructions).</summary>
    [Required]
    public string BeneficiaryName { get; set; } = string.Empty;

    /// <summary>Structured reference prefix (e.g., "GRN" → "GRN-INV-2026-0001").</summary>
    public string ReferencePrefix { get; set; } = "GRN";

    /// <summary>Number of days before a pending transfer is considered expired.</summary>
    public int ExpirationDays { get; set; } = 14;
}

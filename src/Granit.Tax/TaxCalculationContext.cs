namespace Granit.Tax;

/// <summary>
/// Additional tax-domain context for the calculation. Used by the Internal provider
/// to make EU VAT decisions (B2B detection, OSS configuration).
/// </summary>
/// <param name="TransactionType">Classification of the transaction.</param>
/// <param name="IsBusinessToBusiness">Whether the buyer is a business (has valid VAT number).</param>
/// <param name="Exemption">Applied tax exemption reason.</param>
/// <param name="RateCountryCode">Country whose rate should be applied.</param>
public sealed record TaxCalculationContext(
    TransactionType TransactionType,
    bool IsBusinessToBusiness,
    TaxExemptionReason Exemption,
    string RateCountryCode);

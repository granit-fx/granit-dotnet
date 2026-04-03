namespace Granit.Tax;

/// <summary>Result of an online tax ID validation.</summary>
/// <param name="IsValid">Whether the tax ID is valid.</param>
/// <param name="CompanyName">Company name returned by the tax authority (if available).</param>
/// <param name="CompanyAddress">Company address returned by the tax authority (if available).</param>
/// <param name="RequestIdentifier">Consultation number (e.g., VIES request identifier for audit trail).</param>
/// <param name="ValidatedAt">When the validation was performed.</param>
/// <param name="Source">Which system performed the validation.</param>
public sealed record TaxIdValidationResult(
    bool IsValid,
    string? CompanyName,
    string? CompanyAddress,
    string? RequestIdentifier,
    DateTimeOffset? ValidatedAt,
    TaxIdValidationSource Source);

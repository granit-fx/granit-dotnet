using Granit.DataProtection;

namespace Granit.Tax.Domain.ValueObjects;

/// <summary>
/// Company details returned by a tax authority during tax ID validation.
/// </summary>
/// <param name="Name">Company name returned by the tax authority.</param>
/// <param name="Address">Company address returned by the tax authority.</param>
/// <param name="RequestIdentifier">Consultation number for audit trail (e.g., VIES request identifier).</param>
public sealed record CompanyValidationDetails(
    [property: SensitiveData] string? Name,
    [property: SensitiveData(Level = Sensitivity.Confidential)] string? Address,
    string? RequestIdentifier);

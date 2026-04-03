using Granit.DataProtection;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Tax.Domain;

/// <summary>
/// Cached result of an online tax ID validation (VIES, Stripe, HMRC).
/// </summary>
/// <remarks>
/// Results are cached with a configurable TTL (default 24h) to avoid repeated
/// API calls. Entries with <see cref="TaxIdValidationSource.OfflinePending"/>
/// are retried by a background job.
/// </remarks>
public sealed class ValidatedTaxId : Entity, IMultiTenant
{
    private ValidatedTaxId() { }

    /// <summary>Creates a new cached tax ID validation.</summary>
    public static ValidatedTaxId Create(
        Guid id,
        string taxId,
        string countryCode,
        bool isValid,
        TaxIdValidationSource source,
        DateTimeOffset validatedAt,
        DateTimeOffset? expiresAt = null,
        string? companyName = null,
        string? companyAddress = null,
        string? requestIdentifier = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new ValidatedTaxId
        {
            Id = id,
            TaxId = taxId,
            CountryCode = countryCode,
            IsValid = isValid,
            Source = source,
            ValidatedAt = validatedAt,
            ExpiresAt = expiresAt,
            CompanyName = companyName,
            CompanyAddress = companyAddress,
            RequestIdentifier = requestIdentifier,
        };
    }

    /// <summary>The validated tax ID (e.g., "BE0123456789").</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string TaxId { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Whether the tax ID is valid.</summary>
    public bool IsValid { get; private set; }

    /// <summary>Which system performed the validation.</summary>
    public TaxIdValidationSource Source { get; private set; }

    /// <summary>When the validation was performed.</summary>
    public DateTimeOffset ValidatedAt { get; private set; }

    /// <summary>Cache expiry (null = no expiry).</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Company name returned by the tax authority.</summary>
    [SensitiveData]
    public string? CompanyName { get; private set; }

    /// <summary>Company address returned by the tax authority.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? CompanyAddress { get; private set; }

    /// <summary>Consultation number for audit trail (e.g., VIES request identifier).</summary>
    public string? RequestIdentifier { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>Updates the validation after online retry (OfflinePending → Vies).</summary>
    public void ConfirmOnlineValidation(
        bool isValid, TaxIdValidationSource source, DateTimeOffset validatedAt,
        string? companyName, string? companyAddress, string? requestIdentifier)
    {
        IsValid = isValid;
        Source = source;
        ValidatedAt = validatedAt;
        CompanyName = companyName;
        CompanyAddress = companyAddress;
        RequestIdentifier = requestIdentifier;
    }
}

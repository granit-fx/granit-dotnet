using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Tax.Domain;

/// <summary>
/// Admin-managed tax rate override for a specific country.
/// Takes precedence over the default rates from configuration.
/// </summary>
public sealed class TaxRateOverride : Entity, IMultiTenant
{
    private TaxRateOverride() { }

    /// <summary>Creates a new tax rate override.</summary>
    public static TaxRateOverride Create(
        Guid id,
        string countryCode,
        decimal standardRate,
        DateTimeOffset effectiveFrom,
        decimal? reducedRate = null,
        DateTimeOffset? effectiveTo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        return new TaxRateOverride
        {
            Id = id,
            CountryCode = countryCode,
            StandardRate = standardRate,
            ReducedRate = reducedRate,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
        };
    }

    /// <summary>ISO 3166-1 alpha-2 country code.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    /// <summary>Standard tax rate (e.g., 0.21 for 21%).</summary>
    public decimal StandardRate { get; private set; }

    /// <summary>Reduced rate, if applicable.</summary>
    public decimal? ReducedRate { get; private set; }

    /// <summary>When this rate becomes effective.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    /// <summary>When this rate expires (null = current).</summary>
    public DateTimeOffset? EffectiveTo { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }
}

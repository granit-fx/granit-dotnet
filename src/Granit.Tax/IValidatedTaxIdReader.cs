using Granit.Tax.Domain;

namespace Granit.Tax;

/// <summary>Reads cached tax ID validation results (query side of CQRS).</summary>
public interface IValidatedTaxIdReader
{
    /// <summary>Returns a cached validation for a tax ID, or null if not cached or expired.</summary>
    Task<ValidatedTaxId?> GetByTaxIdAsync(string taxId, CancellationToken cancellationToken = default);

    /// <summary>Returns all cached validations for a tenant.</summary>
    Task<IReadOnlyList<ValidatedTaxId>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

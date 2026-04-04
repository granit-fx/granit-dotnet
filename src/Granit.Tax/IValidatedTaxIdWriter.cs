using Granit.Tax.Domain;

namespace Granit.Tax;

/// <summary>Persists tax ID validation results (command side of CQRS).</summary>
public interface IValidatedTaxIdWriter
{
    /// <summary>Caches a validation result.</summary>
    Task AddAsync(ValidatedTaxId entry, CancellationToken cancellationToken = default);

    /// <summary>Updates a cached validation (e.g., OfflinePending → Vies after retry).</summary>
    Task UpdateAsync(ValidatedTaxId entry, CancellationToken cancellationToken = default);
}

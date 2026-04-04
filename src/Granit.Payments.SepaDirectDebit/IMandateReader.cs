using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit;

/// <summary>Reads mandate data (query side of CQRS).</summary>
public interface IMandateReader
{
    /// <summary>Returns a mandate by ID.</summary>
    Task<Mandate?> GetByIdAsync(Guid mandateId, CancellationToken cancellationToken = default);

    /// <summary>Returns a mandate by its unique reference.</summary>
    Task<Mandate?> GetByReferenceAsync(string mandateReference, CancellationToken cancellationToken = default);

    /// <summary>Returns all active mandates for a tenant.</summary>
    Task<IReadOnlyList<Mandate>> GetActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

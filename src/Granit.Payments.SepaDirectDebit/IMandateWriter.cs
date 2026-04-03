using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit;

/// <summary>Persists mandate changes (command side of CQRS).</summary>
public interface IMandateWriter
{
    /// <summary>Persists a new mandate.</summary>
    Task AddAsync(Mandate mandate, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing mandate.</summary>
    Task UpdateAsync(Mandate mandate, CancellationToken cancellationToken = default);
}

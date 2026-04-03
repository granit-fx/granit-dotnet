using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit;

/// <summary>
/// Provider abstraction for SEPA Direct Debit operations.
/// </summary>
public interface IDirectDebitProvider
{
    /// <summary>Provider name (e.g., "internal", "gocardless", "twikey").</summary>
    string Name { get; }

    /// <summary>Sets up a new mandate (creates or initiates redirect flow).</summary>
    Task<MandateSetupResult> SetupMandateAsync(MandateSetupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cancels an active mandate.</summary>
    Task CancelMandateAsync(string providerMandateId, CancellationToken cancellationToken = default);

    /// <summary>Submits a collection against an active mandate.</summary>
    Task<CollectionResult> CollectAsync(CollectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Gets the current status of a collection.</summary>
    Task<CollectionStatusResult> GetCollectionStatusAsync(string providerCollectionId, CancellationToken cancellationToken = default);
}

using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit;

/// <summary>
/// Generates PAIN.008 XML files for submitting direct debit collections to the bank.
/// Self-hosted only — provider-based implementations handle this internally.
/// </summary>
public interface ICollectionFileGenerator
{
    /// <summary>Generates a PAIN.008 batch file from pending collections.</summary>
    Task<CollectionFileResult> GenerateAsync(
        IReadOnlyList<DirectDebitPayment> collections,
        IReadOnlyList<Mandate> mandates,
        CancellationToken cancellationToken = default);
}

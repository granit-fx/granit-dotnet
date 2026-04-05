using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;

namespace Granit.Payments.SepaDirectDebit.Twikey.Internal;

/// <summary>Twikey SEPA DD provider (Phase 3 — stub implementation).</summary>
internal sealed class TwikeyDirectDebitProvider : IDirectDebitProvider
{
    private const string NotImplementedMessage = "Twikey provider not yet implemented.";

    /// <inheritdoc/>
    public string Name => "twikey";

    /// <inheritdoc/>
    public Task<MandateSetupResult> SetupMandateAsync(
        MandateSetupRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NotImplementedMessage);

    /// <inheritdoc/>
    public Task CancelMandateAsync(
        string providerMandateId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NotImplementedMessage);

    /// <inheritdoc/>
    public Task<CollectionResult> CollectAsync(
        CollectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NotImplementedMessage);

    /// <inheritdoc/>
    public Task<CollectionStatusResult> GetCollectionStatusAsync(
        string providerCollectionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(NotImplementedMessage);
}

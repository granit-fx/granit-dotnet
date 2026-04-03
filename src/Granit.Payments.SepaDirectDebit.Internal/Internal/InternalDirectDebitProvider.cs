using Granit.Guids;
using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.Payments.SepaDirectDebit.Internal.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Payments.SepaDirectDebit.Internal.Internal;

/// <summary>
/// Self-hosted SEPA DD provider. Mandates are signed offline (paper/email).
/// Collections are batched into PAIN.008 files for bank submission.
/// </summary>
internal sealed partial class InternalDirectDebitProvider(
    IGuidGenerator guidGenerator,
    ILogger<InternalDirectDebitProvider> logger) : IDirectDebitProvider
{
    /// <inheritdoc/>
    public string Name => "internal";

    /// <inheritdoc/>
    public Task<MandateSetupResult> SetupMandateAsync(
        MandateSetupRequest request, CancellationToken cancellationToken = default)
    {
        string mandateRef = $"SDD-{guidGenerator.Create().ToString("N")[..8].ToUpperInvariant()}";

        Log.MandateCreated(logger, mandateRef);

        return Task.FromResult(new MandateSetupResult(
            ProviderMandateId: mandateRef,
            RedirectUrl: null, // Offline signature
            Status: MandateStatus.Pending));
    }

    /// <inheritdoc/>
    public Task CancelMandateAsync(
        string providerMandateId, CancellationToken cancellationToken = default)
    {
        Log.MandateCancelled(logger, providerMandateId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<CollectionResult> CollectAsync(
        CollectionRequest request, CancellationToken cancellationToken = default)
    {
        string collectionId = $"SDD-COL-{guidGenerator.Create().ToString("N")[..8].ToUpperInvariant()}";

        Log.CollectionCreated(logger, collectionId, request.Amount, request.Currency);

        return Task.FromResult(new CollectionResult(
            ProviderCollectionId: collectionId,
            Status: CollectionStatus.Pending));
    }

    /// <inheritdoc/>
    public Task<CollectionStatusResult> GetCollectionStatusAsync(
        string providerCollectionId, CancellationToken cancellationToken = default)
    {
        // Status is updated by reconciliation (CAMT.053 import), not by querying an API
        return Task.FromResult(new CollectionStatusResult(
            CollectionStatus.Pending, FailureCode: null, FailureReason: null));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "SDD mandate created: {Reference}")]
        public static partial void MandateCreated(ILogger logger, string reference);

        [LoggerMessage(Level = LogLevel.Information, Message = "SDD mandate cancelled: {MandateId}")]
        public static partial void MandateCancelled(ILogger logger, string mandateId);

        [LoggerMessage(Level = LogLevel.Information, Message = "SDD collection created: {CollectionId}, {Amount} {Currency}")]
        public static partial void CollectionCreated(ILogger logger, string collectionId, decimal amount, string currency);
    }
}

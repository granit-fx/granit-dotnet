using System.Diagnostics;
using Granit.Encryption.CryptoShredding;
using Granit.Encryption.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Encryption.Services;

/// <summary>
/// Default implementation of <see cref="ICryptoShredder"/> that delegates key
/// destruction to <see cref="IEntityEncryptionKeyStore"/> and notifies all
/// registered <see cref="ICryptoShreddingAuditRecorder"/> instances.
/// </summary>
/// <remarks>
/// Erasure is two-phase and recoverable: a durable <see cref="CryptoShreddingPhase.Requested"/> intent
/// record is written <em>before</em> the irreversible <see cref="IEntityEncryptionKeyStore.DeleteKeyAsync"/>,
/// and a <see cref="CryptoShreddingPhase.Confirmed"/> record is written afterwards. The destruction never
/// precedes a durable record, so an audit failure cannot leave an erasure without a trail (GDPR Art. 5(2)).
/// </remarks>
public sealed partial class DefaultCryptoShredder(
    IEntityEncryptionKeyStore keyStore,
    TimeProvider timeProvider,
    EncryptionMetrics metrics,
    ILogger<DefaultCryptoShredder> logger,
    IEnumerable<ICryptoShreddingAuditRecorder> auditRecorders) : ICryptoShredder
{
    public async Task ShredAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.Shred);
        activity?.SetTag("entity_type", entityType);
        activity?.SetTag("entity_id", entityId);

        await ShredOneAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
    }

    public async Task ShredBatchAsync(
        string entityType,
        IEnumerable<string> entityIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentNullException.ThrowIfNull(entityIds);

        using Activity? activity = EncryptionActivitySource.Source.StartActivity(
            EncryptionActivitySource.ShredBatch);
        activity?.SetTag("entity_type", entityType);

        int count = 0;

        foreach (string entityId in entityIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ShredOneAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
            count++;
        }

        LogBatchShredded(logger, entityType, count);
    }

    private async Task ShredOneAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        // Phase 1 — durable intent BEFORE the irreversible destruction. If this throws, no key is
        // destroyed, so the operation can be retried without ever having erased data trail-less.
        DateTimeOffset requestedAt = timeProvider.GetUtcNow();
        await NotifyAuditRecordersAsync(
            entityType, entityId, CryptoShreddingPhase.Requested, requestedAt, cancellationToken)
            .ConfigureAwait(false);

        // Phase 2 — the irreversible operation, now backed by a durable intent record.
        await keyStore.DeleteKeyAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
        metrics.RecordKeyShredded(null, entityType);
        LogKeyShredded(logger, entityType, entityId);

        // Phase 3 — confirmation. A confirmation failure leaves the intent record standing, so the
        // erasure remains auditable and reconcilable.
        DateTimeOffset confirmedAt = timeProvider.GetUtcNow();
        await NotifyAuditRecordersAsync(
            entityType, entityId, CryptoShreddingPhase.Confirmed, confirmedAt, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task NotifyAuditRecordersAsync(
        string entityType,
        string entityId,
        CryptoShreddingPhase phase,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        List<Exception>? failures = null;

        // Fan out to every recorder even if one throws: a single failing recorder must not deprive the
        // others of the record, and the aggregated failure still surfaces to the caller.
        foreach (ICryptoShreddingAuditRecorder recorder in auditRecorders)
        {
            try
            {
                await recorder.RecordAsync(entityType, entityId, phase, occurredAt, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                (failures ??= []).Add(ex);
                LogAuditRecorderFailed(logger, recorder.GetType().Name, phase, entityType, entityId, ex);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException(
                $"{failures.Count} crypto-shredding audit recorder(s) failed to record the " +
                $"'{phase}' phase for {entityType}/{entityId}.",
                failures);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Per-entity encryption key destroyed for {EntityType}/{EntityId} — data is permanently unreadable")]
    private static partial void LogKeyShredded(ILogger logger, string entityType, string entityId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Batch crypto-shredding completed for {EntityType}: {Count} keys destroyed")]
    private static partial void LogBatchShredded(ILogger logger, string entityType, int count);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Crypto-shredding audit recorder {RecorderType} failed for the {Phase} phase of {EntityType}/{EntityId}")]
    private static partial void LogAuditRecorderFailed(
        ILogger logger,
        string recorderType,
        CryptoShreddingPhase phase,
        string entityType,
        string entityId,
        Exception exception);
}

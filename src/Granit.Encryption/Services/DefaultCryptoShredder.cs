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

        await keyStore.DeleteKeyAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset shreddedAt = timeProvider.GetUtcNow();
        metrics.RecordKeyShredded(null, entityType);
        LogKeyShredded(logger, entityType, entityId);

        await NotifyAuditRecordersAsync(entityType, entityId, shreddedAt, cancellationToken)
            .ConfigureAwait(false);
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

        DateTimeOffset shreddedAt = timeProvider.GetUtcNow();
        int count = 0;

        foreach (string entityId in entityIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await keyStore.DeleteKeyAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
            metrics.RecordKeyShredded(null, entityType);

            await NotifyAuditRecordersAsync(entityType, entityId, shreddedAt, cancellationToken)
                .ConfigureAwait(false);

            count++;
        }

        LogBatchShredded(logger, entityType, count);
    }

    private async Task NotifyAuditRecordersAsync(
        string entityType,
        string entityId,
        DateTimeOffset shreddedAt,
        CancellationToken cancellationToken)
    {
        foreach (ICryptoShreddingAuditRecorder recorder in auditRecorders)
        {
            await recorder.RecordAsync(entityType, entityId, shreddedAt, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Per-entity encryption key destroyed for {EntityType}/{EntityId} — data is permanently unreadable")]
    private static partial void LogKeyShredded(ILogger logger, string entityType, string entityId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Batch crypto-shredding completed for {EntityType}: {Count} keys destroyed")]
    private static partial void LogBatchShredded(ILogger logger, string entityType, int count);
}

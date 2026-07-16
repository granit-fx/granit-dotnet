using System.Diagnostics;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Events;
using Granit.Events;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Single choke point for audit entry persistence. Every write path — embedded staging into
/// the audited host transaction, standalone post-commit persistence, and explicit
/// <see cref="IAuditingWriter"/> writes — flows through here, so metrics recording and
/// <see cref="AuditEntryPersistedEto"/> emission cannot drift between paths and the event
/// always carries the real persisted <c>AuditEntry.Id</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Embedded mode</b> (<see cref="StageAsync"/> + <see cref="OnCommitted"/>): the audit
/// graph is added to the audited host context during <c>SavingChanges</c> and rides the same
/// transaction as the business mutation — a rollback removes the audit rows with it. The
/// <see cref="AuditEntryPersistedEto"/> is dispatched pre-commit through
/// <see cref="IIntegrationEventDispatcher"/>, the same timing as every Granit integration
/// event, so a Wolverine-backed host writes the outbox envelope atomically with the entry.
/// </para>
/// <para>
/// <b>Standalone mode</b> (<see cref="PersistAsync"/>): the entry is saved through the
/// isolated <see cref="AuditingDbContext"/> in its own transaction; the event is dispatched
/// only after the save succeeds — the row is the source of truth, the event is best-effort.
/// </para>
/// </remarks>
internal sealed partial class AuditPersistencePipeline(
    IDbContextFactory<AuditingDbContext> dbContextFactory,
    IIntegrationEventDispatcher integrationEventDispatcher,
    IGuidGenerator guidGenerator,
    AuditingMetrics metrics,
    ILogger<AuditPersistencePipeline> logger)
{
    /// <summary>
    /// Embedded path: adds the audit graph to the ongoing host <c>SaveChanges</c> (pre-commit)
    /// and dispatches the <see cref="AuditEntryPersistedEto"/> into the integration-event
    /// pipeline so a Wolverine outbox envelope commits atomically with the audit row.
    /// </summary>
    public async ValueTask StageAsync(DbContext hostContext, AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hostContext);
        ArgumentNullException.ThrowIfNull(entry);

        hostContext.Add(entry);

        await integrationEventDispatcher
            .DispatchAsync([ToEto(entry)], cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Embedded path, called after the host commit succeeded: records metrics for the entry
    /// that was staged by <see cref="StageAsync"/>.
    /// </summary>
    public void OnCommitted(AuditEntry entry, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string? tenantId = entry.TenantId?.ToString();
        metrics.RecordPersistenceDuration(elapsed.TotalMilliseconds, tenantId, AuditingMetrics.EmbeddedMode);
        metrics.RecordEntityChangeCount(entry.EntityChanges.Count, tenantId);
        metrics.RecordPersisted(1, tenantId);
        LogEntryPersisted(entry.EntityChanges.Count, AuditingMetrics.EmbeddedMode);
    }

    /// <summary>
    /// Standalone path (capture fallback and explicit <see cref="IAuditingWriter"/> writes):
    /// ensures the entry id, saves through the isolated <see cref="AuditingDbContext"/>, then
    /// dispatches the <see cref="AuditEntryPersistedEto"/> and records metrics. Returns the
    /// persisted entry with its id populated.
    /// </summary>
    public async Task<AuditEntry> PersistAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        long startTimestamp = Stopwatch.GetTimestamp();

        // Assign the id eagerly (instead of relying on the AuditedEntityInterceptor stamp
        // mid-save) so the event and the caller's instance always carry the persisted id.
        if (entry.Id == Guid.Empty)
        {
            entry.Id = guidGenerator.Create();
        }

        await using AuditingDbContext dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        string? tenantId = entry.TenantId?.ToString();
        metrics.RecordPersistenceDuration(
            Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds,
            tenantId,
            AuditingMetrics.StandaloneMode);
        metrics.RecordEntityChangeCount(entry.EntityChanges.Count, tenantId);
        metrics.RecordPersisted(1, tenantId);
        LogEntryPersisted(entry.EntityChanges.Count, AuditingMetrics.StandaloneMode);

        // Dispatch after the save: never an event for a row that failed to persist.
        await integrationEventDispatcher
            .DispatchAsync([ToEto(entry)], cancellationToken)
            .ConfigureAwait(false);

        return entry;
    }

    private static AuditEntryPersistedEto ToEto(AuditEntry entry) => new(
        entry.Id,
        entry.Timestamp,
        entry.UserId,
        entry.Category,
        entry.EntityChanges.Count,
        entry.TenantId,
        entry.EntityChanges.FirstOrDefault()?.EntityType);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Audit entry persisted with {EntityChangeCount} entity changes ({Mode} mode)")]
    private partial void LogEntryPersisted(int entityChangeCount, string mode);
}

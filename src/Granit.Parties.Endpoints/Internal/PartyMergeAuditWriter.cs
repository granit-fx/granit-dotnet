using System.Text.Json;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;

namespace Granit.Parties.Endpoints.Internal;

/// <summary>
/// Writes a Party-merge audit entry after a successful live merge. Captures who merged
/// what, with which choices, what reason was given, and how many rows each cross-module
/// rewriter touched.
/// </summary>
/// <remarks>
/// <para>
/// Why endpoint-level rather than an <c>ILocalEventHandler&lt;PartyMergedEvent&gt;</c>:
/// the orchestrator wire-up that fires <c>PartyMergedEvent</c> is still pending (see
/// <c>Party.RaiseMergedEvents</c> from #1286). Until that lands, a domain-event handler
/// would never fire. Writing directly from the endpoint covers the audit need today
/// without depending on the orchestrator change. The handler-based path remains a clean
/// future migration: move this logic to a handler, drop the endpoint call, no schema
/// change.
/// </para>
/// <para>
/// <b>Failure semantics.</b> The audit write happens <em>after</em> the merge transaction
/// commits, so an audit-DB outage at this exact moment loses the audit entry while
/// keeping the merge. That's the standard trade-off the framework already accepts for
/// post-commit handlers — the alternative (rolling back a successful merge because the
/// audit DB is unreachable) is worse. The merge endpoint logs and continues if writing
/// fails — the merge result is still returned to the caller.
/// </para>
/// </remarks>
internal sealed class PartyMergeAuditWriter(
    IAuditingWriter auditingWriter,
    ICurrentUserService currentUser,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IClock clock)
{
    /// <summary>
    /// Records a successful Party merge. Composes one <see cref="AuditEntry"/> with a
    /// single <see cref="AuditEntityChange"/> on the survivor (<c>EntityType="Party"</c>,
    /// <c>ChangeType=Modified</c>) plus four property changes that capture the merge
    /// payload in a stable, queryable shape:
    /// <list type="bullet">
    /// <item><c>MergedFromId</c> — the loser id (string).</item>
    /// <item><c>Reason</c> — the operator's free-form justification (or <c>null</c>).</item>
    /// <item><c>ResolvedChoices</c> — JSON dictionary of every <c>fieldPath → "Survivor"|"Loser"</c>.</item>
    /// <item><c>RewriteCounts</c> — JSON dictionary of every <c>rewriter.Description → rowsAffected</c>.</item>
    /// </list>
    /// </summary>
    public async Task RecordAsync(
        Guid survivorId,
        Guid loserId,
        IReadOnlyDictionary<string, string>? resolvedChoices,
        IReadOnlyDictionary<string, int> rewriteCounts,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rewriteCounts);

        AuditEntityChange change = new()
        {
            Id = guidGenerator.Create(),
            EntityType = "Party",
            EntityId = survivorId.ToString(),
            ChangeType = AuditChangeType.Modified,
            PropertyChanges =
            [
                new AuditPropertyChange
                {
                    Id = guidGenerator.Create(),
                    PropertyName = "MergedFromId",
                    OriginalValue = null,
                    NewValue = loserId.ToString(),
                },
                new AuditPropertyChange
                {
                    Id = guidGenerator.Create(),
                    PropertyName = "Reason",
                    OriginalValue = null,
                    NewValue = reason,
                },
                new AuditPropertyChange
                {
                    Id = guidGenerator.Create(),
                    PropertyName = "ResolvedChoices",
                    OriginalValue = null,
                    NewValue = JsonSerializer.Serialize(
                        resolvedChoices ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                },
                new AuditPropertyChange
                {
                    Id = guidGenerator.Create(),
                    PropertyName = "RewriteCounts",
                    OriginalValue = null,
                    NewValue = JsonSerializer.Serialize(rewriteCounts),
                },
            ],
        };

        AuditEntry entry = new()
        {
            Id = guidGenerator.Create(),
            Timestamp = clock.Now,
            UserId = currentUser.UserId ?? "system",
            UserName = currentUser.UserName,
            Category = AuditCategory.DataMutation,
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            CorrelationId = System.Diagnostics.Activity.Current?.Id,
            EntityChanges = [change],
        };

        change.AuditEntryId = entry.Id;
        foreach (AuditPropertyChange p in change.PropertyChanges)
        {
            p.AuditEntityChangeId = change.Id;
        }

        await auditingWriter.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}

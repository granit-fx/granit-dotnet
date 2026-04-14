using Granit.AI.Diagnostics;
using Granit.AI.EntityFrameworkCore.Entities;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IAIUsageTracker"/>.
/// </summary>
/// <remarks>
/// Persists usage records for billing, cost monitoring, and ISO 27001 audit trail.
/// Records are immutable after creation.
/// </remarks>
internal sealed class EfAIUsageStore(
    IDbContextFactory<AIDbContext> contextFactory,
    ICurrentTenant currentTenant,
    AIMetrics metrics)
    : EfStoreBase<AIUsageRecordEntity, AIDbContext>(contextFactory, currentTenant), IAIUsageTracker
{
    /// <inheritdoc/>
    public async Task RecordAsync(
        AIUsageRecord record,
        CancellationToken cancellationToken = default)
    {
        var entity = AIUsageRecordEntity.FromRecord(record);
        await AddAsync(entity, cancellationToken).ConfigureAwait(false);

        string? tenantId = record.TenantId?.ToString();
        metrics.RecordRequestCompleted(tenantId, record.Model, record.Provider, "success");
        metrics.RecordTokensUsed(tenantId, record.Model, record.Provider, record.InputTokens, record.OutputTokens);

        if (record.Duration.HasValue)
        {
            metrics.RecordRequestDuration(tenantId, record.Model, record.Provider, record.Duration.Value);
        }
    }
}

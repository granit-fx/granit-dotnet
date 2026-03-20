using Granit.AI.Diagnostics;
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
    AIMetrics metrics) : IAIUsageTracker
{
    /// <inheritdoc/>
    public async Task RecordAsync(
        AIUsageRecord record,
        CancellationToken cancellationToken = default)
    {
        await using AIDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = AIUsageRecordEntity.FromRecord(record);
        context.UsageRecords.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        string? tenantId = record.TenantId?.ToString();
        metrics.RecordRequestCompleted(tenantId, record.Model, record.Provider, "success");
        metrics.RecordTokensUsed(tenantId, record.Model, record.Provider, record.InputTokens, record.OutputTokens);

        if (record.Duration.HasValue)
        {
            metrics.RecordRequestDuration(tenantId, record.Model, record.Provider, record.Duration.Value);
        }
    }
}

using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IAIUsageQueryableProvider"/>.
/// Exposes <see cref="IQueryable{T}"/> access to usage records via <see cref="AIDbContext"/>.
/// </summary>
internal sealed class EfAIUsageQueryableProvider(IDbContextFactory<AIDbContext> contextFactory)
    : IAIUsageQueryableProvider
{
    private readonly AIDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<AIUsageRecord> GetUsageRecords() =>
        _context.UsageRecords
            .AsNoTracking()
            .Select(e => new AIUsageRecord
            {
                Id = e.Id,
                TenantId = e.TenantId,
                UserId = e.UserId,
                WorkspaceName = e.WorkspaceName,
                Provider = e.Provider,
                Model = e.Model,
                InputTokens = e.InputTokens,
                OutputTokens = e.OutputTokens,
                EstimatedCostUsd = e.EstimatedCostUsd,
                Timestamp = e.CreatedAt,
                Duration = e.Duration,
            });
}

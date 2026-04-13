using Granit.AI.EntityFrameworkCore.Entities;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AIUsageRecord"/>.
/// Projects internal <see cref="AIUsageRecordEntity"/> to the public <see cref="AIUsageRecord"/> DTO.
/// </summary>
internal sealed class EfAIUsageQueryableSource(IDbContextFactory<AIDbContext> contextFactory)
    : IQueryableSource<AIUsageRecord>
{
    private readonly AIDbContext _context = contextFactory.CreateDbContext();

    public IQueryable<AIUsageRecord> GetQueryable() =>
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

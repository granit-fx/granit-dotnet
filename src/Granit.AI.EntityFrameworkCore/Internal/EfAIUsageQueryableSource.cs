using Granit.AI.EntityFrameworkCore.Entities;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AIUsageRecord"/>.
/// Projects internal <see cref="AIUsageRecordEntity"/> to the public <see cref="AIUsageRecord"/> DTO.
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all usage records are returned cross-tenant.
/// </summary>
internal sealed class EfAIUsageQueryableSource(
    IDbContextFactory<AIDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<AIUsageRecord>
{
    private readonly AIDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<AIUsageRecord> GetQueryable()
    {
        IQueryable<AIUsageRecordEntity> query = _context.UsageRecords.AsNoTracking();
        if (_bypassTenantFilter)
        {
            query = query.IgnoreQueryFilters([GranitFilterNames.MultiTenant]);
        }

        return query.Select(e => new AIUsageRecord
        {
            Id = e.Id,
            TenantId = e.TenantId,
            UserId = e.UserId,
            WorkspaceName = e.WorkspaceName,
            Provider = e.Provider,
            Model = e.Model,
            InputTokens = e.InputTokens,
            OutputTokens = e.OutputTokens,
            EstimatedCost = e.EstimatedCost,
            CostCurrency = e.CostCurrency,
            Timestamp = e.CreatedAt,
            Duration = e.Duration,
            PromptVersion = e.PromptVersion,
            PromptTemplateName = e.PromptTemplateName,
            PromptTemplateVersion = e.PromptTemplateVersion,
        });
    }
}

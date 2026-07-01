using Granit.AI.EntityFrameworkCore.Entities;
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
    ITenantQueryScope scope)
    : IQueryableSource<AIUsageRecord>, IAsyncDisposable, IDisposable
{
    private AIDbContext? _context;

    public IQueryable<AIUsageRecord> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<AIUsageRecordEntity> query =
            scope.Restrict(_context.UsageRecords.AsNoTracking(), typeof(AIUsageRecordEntity).Name);

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
            ConversationId = e.ConversationId,
            PromptVersion = e.PromptVersion,
            PromptTemplateName = e.PromptTemplateName,
            PromptTemplateVersion = e.PromptTemplateVersion,
        });
    }

    public ValueTask DisposeAsync()
    {
        AIDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}

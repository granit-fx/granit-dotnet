using Granit.AI.Prompts.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IQueryableSource{TEntity}"/> for <see cref="PromptTemplate"/>, backing the
/// catalogue admin grid and export. When no tenant context is active (host admin) the multi-tenant
/// query filter is bypassed so prompts are returned cross-tenant.
/// </summary>
internal sealed class EfPromptTemplateQueryableSource(
    IDbContextFactory<AIPromptsDbContext> contextFactory,
    ICurrentTenant currentTenant) : IQueryableSource<PromptTemplate>
{
    private readonly AIPromptsDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<PromptTemplate> GetQueryable()
    {
        IQueryable<PromptTemplate> query = _context.PromptTemplates.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }
}

using Granit.AI.Prompts.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IQueryableSource{TEntity}"/> for <see cref="PromptTemplate"/>, backing the
/// catalogue admin grid and export. When no tenant context is active (host admin, or a
/// single-tenant deployment with no resolver) the multi-tenant query filter is bypassed so prompts
/// are returned cross-tenant.
/// </summary>
/// <remarks>
/// SECURITY CONTRACT: the cross-tenant bypass is gated <em>upstream</em>, not here. This source is
/// reachable only through the admin <c>PromptTemplateQueryDefinition</c> / <c>PromptTemplateExportDefinition</c>,
/// which the query/export engine binds to a host-admin permission. A tenant-scoped request always
/// carries an available tenant (<see cref="ICurrentTenant.IsAvailable"/> is <see langword="true"/>),
/// so the filter is never bypassed for it; only a request with no resolved tenant — by definition not
/// a tenant user — sees across tenants. Do not consume this source from a tenant-facing endpoint.
/// </remarks>
internal sealed class EfPromptTemplateQueryableSource(
    IDbContextFactory<AIPromptsDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<PromptTemplate>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private AIPromptsDbContext? _context;

    public IQueryable<PromptTemplate> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<PromptTemplate> query = _context.PromptTemplates.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public ValueTask DisposeAsync()
    {
        AIPromptsDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}

using Granit.AI.Prompts.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core <see cref="IQueryableSource{TEntity}"/> for <see cref="PromptTemplate"/>, backing the
/// catalogue admin grid and export. When no tenant context is active, the multi-tenant query
/// filter is bypassed cross-tenant <b>only</b> for a signaled host-access request (fail-closed
/// otherwise) via <see cref="ITenantQueryScope"/>.
/// </summary>
/// <remarks>
/// SECURITY CONTRACT: cross-tenant visibility is fail-closed by default. An absent tenant context
/// widens the query to all tenants only when the request carries an explicit host-access signal
/// (<c>.AllowHostAccess()</c>); an unsignaled absence is restricted to the host partition and
/// logged. Reachable only through the admin <c>PromptTemplateQueryDefinition</c> /
/// <c>PromptTemplateExportDefinition</c>; do not consume this source from a tenant-facing endpoint.
/// </remarks>
internal sealed class EfPromptTemplateQueryableSource(
    IDbContextFactory<AIPromptsDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<PromptTemplate>, IAsyncDisposable, IDisposable
{
    private AIPromptsDbContext? _context;

    public IQueryable<PromptTemplate> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<PromptTemplate> query = _context.PromptTemplates.AsNoTracking();
        return scope.Restrict(query, typeof(PromptTemplate).Name);
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

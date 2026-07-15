using Granit.DataExchange.Export;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.Models;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="OpenIddictScopeModel"/>, backing <c>MapGranitQuery</c> and the analytics runner
/// over <c>ScopeQuery</c>. Applies the multi-tenant query filter on the OpenIddict-managed entity
/// set (registered via <c>ReplaceDefaultEntities</c>) and projects to the framework-owned model so
/// the base module carries no EF dependency. When no tenant context is active (host admin), the
/// multi-tenant query filter is bypassed.
/// </summary>
internal sealed class EfGranitOpenIddictScopeQueryableSource(
    IDbContextFactory<OpenIddictDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<OpenIddictScopeModel>, IExportDataSource<OpenIddictScopeModel>, IAsyncDisposable, IDisposable
{
    private OpenIddictDbContext? _context;

    public IQueryable<OpenIddictScopeModel> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<GranitOpenIddictScope> query =
            _context.Set<GranitOpenIddictScope>().AsNoTracking();
        query = scope.Restrict(query, typeof(GranitOpenIddictScope).Name);

        return query.Select(s => new OpenIddictScopeModel
        {
            Id = s.Id,
            TenantId = s.TenantId,
            Name = s.Name,
            DisplayName = s.DisplayName,
            Description = s.Description,
            Properties = s.Properties,
            Resources = s.Resources,
        });
    }

    public ValueTask DisposeAsync()
    {
        OpenIddictDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}

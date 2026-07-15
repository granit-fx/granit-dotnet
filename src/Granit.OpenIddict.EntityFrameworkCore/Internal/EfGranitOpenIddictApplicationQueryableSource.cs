using Granit.DataExchange.Export;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.Models;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="OpenIddictApplicationModel"/>, backing <c>MapGranitQuery</c> and the analytics
/// runner over <c>ApplicationQuery</c>. Applies the multi-tenant query filter on the
/// OpenIddict-managed entity set (registered via <c>ReplaceDefaultEntities</c>) and projects to
/// the framework-owned model so the base module carries no EF dependency. When no tenant context
/// is active (host admin), the multi-tenant query filter is bypassed.
/// </summary>
internal sealed class EfGranitOpenIddictApplicationQueryableSource(
    IDbContextFactory<OpenIddictDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<OpenIddictApplicationModel>, IExportDataSource<OpenIddictApplicationModel>, IAsyncDisposable, IDisposable
{
    private OpenIddictDbContext? _context;

    public IQueryable<OpenIddictApplicationModel> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<GranitOpenIddictApplication> query =
            _context.Set<GranitOpenIddictApplication>().AsNoTracking();
        query = scope.Restrict(query, typeof(GranitOpenIddictApplication).Name);

        return query.Select(a => new OpenIddictApplicationModel
        {
            Id = a.Id,
            TenantId = a.TenantId,
            ClientId = a.ClientId,
            DisplayName = a.DisplayName,
            ClientType = a.ClientType,
            ConsentType = a.ConsentType,
            ApplicationType = a.ApplicationType,
            JsonWebKeySet = a.JsonWebKeySet,
            Properties = a.Properties,
            Permissions = a.Permissions,
            RedirectUris = a.RedirectUris,
            PostLogoutRedirectUris = a.PostLogoutRedirectUris,
            Requirements = a.Requirements,
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

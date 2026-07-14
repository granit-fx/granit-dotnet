using Granit.Http.Cookies.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Http.Cookies.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="CookieConsentRecord"/>. When no tenant context is active (host admin),
/// the multi-tenant query filter is bypassed so consent statistics can be reviewed
/// cross-tenant — required for group-level GDPR accountability reporting.
/// </summary>
internal sealed class EfCookieConsentRecordQueryableSource(
    IDbContextFactory<CookiesDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<CookieConsentRecord>, IAsyncDisposable, IDisposable
{
    private CookiesDbContext? _context;

    public IQueryable<CookieConsentRecord> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<CookieConsentRecord> query = _context.ConsentRecords.AsNoTracking();
        return scope.Restrict(query, typeof(CookieConsentRecord).Name);
    }

    public ValueTask DisposeAsync()
    {
        CookiesDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}

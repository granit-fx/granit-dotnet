using Granit.Auditing.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AuditEntry"/>.
/// When no tenant context is active (host admin), the multi-tenant query filter is bypassed
/// so all audit entries are returned cross-tenant — required for ISO 27001 audit trail review.
/// </summary>
internal sealed class EfAuditEntryQueryableSource(
    IDbContextFactory<AuditingDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<AuditEntry>, IAsyncDisposable, IDisposable
{
    private AuditingDbContext? _context;

    public IQueryable<AuditEntry> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<AuditEntry> query = _context.AuditEntries.AsNoTracking();
        return scope.Restrict(query, typeof(AuditEntry).Name);
    }

    public ValueTask DisposeAsync()
    {
        AuditingDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}

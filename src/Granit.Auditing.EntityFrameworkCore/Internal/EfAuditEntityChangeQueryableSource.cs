using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AuditEntityChange"/>.
/// Joins through the parent <see cref="AuditEntry"/>'s tenant filter — when no tenant context
/// is active (host admin), the multi-tenant filter on the parent is bypassed so all entity
/// changes are returned cross-tenant.
/// </summary>
internal sealed class EfAuditEntityChangeQueryableSource(
    IDbContextFactory<AuditingDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<AuditEntityChange>, IAsyncDisposable, IDisposable
{
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;
    private AuditingDbContext? _context;

    public IQueryable<AuditEntityChange> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<AuditEntityChange> query = _context.AuditEntityChanges.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
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

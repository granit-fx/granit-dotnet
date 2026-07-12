using Granit.Auditing.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="AuditEntityChange"/>.
/// The entity is <c>IMultiTenant</c> (tenant id denormalized from the parent
/// <see cref="AuditEntry"/>), so the standard multi-tenant query filter applies directly and
/// <see cref="ITenantQueryScope.Restrict"/> scopes tenant-bound callers; host admins with no
/// resolved tenant read cross-tenant per the scope's fail-closed rules.
/// </summary>
internal sealed class EfAuditEntityChangeQueryableSource(
    IDbContextFactory<AuditingDbContext> contextFactory,
    ITenantQueryScope scope)
    : IQueryableSource<AuditEntityChange>, IAsyncDisposable, IDisposable
{
    private AuditingDbContext? _context;

    public IQueryable<AuditEntityChange> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        IQueryable<AuditEntityChange> query = _context.AuditEntityChanges.AsNoTracking();
        return scope.Restrict(query, typeof(AuditEntityChange).Name);
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

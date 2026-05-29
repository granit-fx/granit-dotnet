using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="AuditEntry"/>. Dispatches through <see cref="AuditingContextResolver"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared mode.</b> Opens the host context (single physical table). Host-admin scope
/// bypasses the MultiTenant query filter for cross-tenant ISO 27001 / SOC2 review.
/// </para>
/// <para>
/// <b>Segregated + tenant scope.</b> Opens the tenant-isolated context only.
/// </para>
/// <para>
/// <b>Segregated + host-admin scope.</b> Materialises across the host context plus every
/// tenant returned by <see cref="ITenantsAccessor"/> via <see cref="ICurrentTenant.Change"/>,
/// then exposes the result as an in-memory <see cref="IQueryable{T}"/> — required for the
/// cross-tenant SOC2 trail review the audit log exists to support.
/// </para>
/// </remarks>
internal sealed class EfAuditEntryQueryableSource : IQueryableSource<AuditEntry>, IDisposable
{
    private readonly AuditingContextResolver _resolver;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<AuditingHostDbContext> _hostFactory;
    private readonly IDbContextFactory<AuditingTenantDbContext>? _tenantFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantsAccessor _tenantsAccessor;
    private DbContext? _context;
    private List<AuditEntry>? _materialized;

    public EfAuditEntryQueryableSource(
        AuditingContextResolver resolver,
        ICurrentTenant currentTenant,
        ITenantsAccessor tenantsAccessor,
        IDbContextFactory<AuditingHostDbContext> hostFactory,
        IDbContextFactory<AuditingTenantDbContext>? tenantFactory = null)
    {
        _resolver = resolver;
        _bypassTenantFilter = !currentTenant.IsAvailable;
        _hostFactory = hostFactory;
        _tenantFactory = tenantFactory;
        _currentTenant = currentTenant;
        _tenantsAccessor = tenantsAccessor;
    }

    public IQueryable<AuditEntry> GetQueryable()
    {
        if (_resolver.StorageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            _materialized ??= MaterialiseAcrossAllTenants();
            return _materialized.AsQueryable();
        }

        _context ??= OpenContext();

        IQueryable<AuditEntry> query = ((IAuditingDbContext)_context).AuditEntries.AsNoTracking();
        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public void Dispose() => _context?.Dispose();

    private DbContext OpenContext() => _resolver.StorageMode switch
    {
        DualScopeStorageMode.Shared => _hostFactory.CreateDbContext(),
        DualScopeStorageMode.Segregated => _tenantFactory!.CreateDbContext(),
        _ => throw new InvalidOperationException($"Unknown DualScopeStorageMode: {_resolver.StorageMode}."),
    };

    private List<AuditEntry> MaterialiseAcrossAllTenants()
    {
        List<AuditEntry> results = [];

        using (AuditingHostDbContext host = _hostFactory.CreateDbContext())
        {
            results.AddRange(host.AuditEntries
                .AsNoTracking()
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .ToList());
        }

        if (_tenantFactory is null)
        {
            return results;
        }

        IReadOnlyList<(Guid Id, string Name)> tenants = _tenantsAccessor
            .GetAllAsync().GetAwaiter().GetResult();

        foreach ((Guid id, string name) in tenants)
        {
            using (_currentTenant.Change(id, name))
            using (AuditingTenantDbContext tenantCtx = _tenantFactory.CreateDbContext())
            {
                results.AddRange(tenantCtx.AuditEntries.AsNoTracking().ToList());
            }
        }

        return results;
    }
}

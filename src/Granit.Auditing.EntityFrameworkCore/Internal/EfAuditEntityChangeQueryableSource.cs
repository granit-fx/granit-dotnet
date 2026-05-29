using Granit.Auditing.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="AuditEntityChange"/>. Joins through the parent <see cref="AuditEntry"/>'s
/// tenant filter. Dispatches through <see cref="AuditingContextResolver"/> — same
/// host/tenant/cross-tenant routing as <see cref="EfAuditEntryQueryableSource"/>.
/// </summary>
internal sealed class EfAuditEntityChangeQueryableSource : IQueryableSource<AuditEntityChange>, IDisposable
{
    private readonly AuditingContextResolver _resolver;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<AuditingHostDbContext> _hostFactory;
    private readonly IDbContextFactory<AuditingTenantDbContext>? _tenantFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantsAccessor _tenantsAccessor;
    private DbContext? _context;
    private List<AuditEntityChange>? _materialized;

    public EfAuditEntityChangeQueryableSource(
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

    public IQueryable<AuditEntityChange> GetQueryable()
    {
        if (_resolver.StorageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            _materialized ??= MaterialiseAcrossAllTenants();
            return _materialized.AsQueryable();
        }

        _context ??= OpenContext();

        IQueryable<AuditEntityChange> query = ((IAuditingDbContext)_context).AuditEntityChanges.AsNoTracking();
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

    private List<AuditEntityChange> MaterialiseAcrossAllTenants()
    {
        List<AuditEntityChange> results = [];

        using (AuditingHostDbContext host = _hostFactory.CreateDbContext())
        {
            results.AddRange(host.AuditEntityChanges
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
                results.AddRange(tenantCtx.AuditEntityChanges.AsNoTracking().ToList());
            }
        }

        return results;
    }
}

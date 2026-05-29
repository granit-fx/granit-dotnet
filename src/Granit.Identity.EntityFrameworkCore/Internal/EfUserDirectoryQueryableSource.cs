using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Options;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserDirectoryQueryableSource"/>. Returns an
/// <see cref="IQueryable{User}"/> over the <c>Users</c> set with the framework conventions
/// (tenant + soft-delete) already woven into the model.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared mode.</b> Opens the host context (which under <c>Shared</c> holds every user).
/// When no tenant is active (host admin), the
/// <see cref="GranitFilterNames.MultiTenant"/> filter is bypassed so all users are returned
/// cross-tenant.
/// </para>
/// <para>
/// <b>Segregated + tenant scope.</b> Opens the tenant-isolated context only.
/// </para>
/// <para>
/// <b>Segregated + host-admin scope.</b> Materialises across the host context plus every
/// tenant returned by <see cref="ITenantsAccessor"/> via <see cref="ICurrentTenant.Change"/>,
/// then exposes the result as an in-memory <see cref="IQueryable{T}"/>. Filters, ordering
/// and paging applied by <c>Granit.QueryEngine</c> downstream run in LINQ-to-Objects rather
/// than translating to SQL — acceptable for admin browse volumes. A Postgres cross-schema
/// materialised view is the documented optimisation path for deployments with very large
/// user counts per tenant.
/// </para>
/// <para>
/// Soft-dep on <see cref="ITenantsAccessor"/>: the default <c>NullTenantsAccessor</c>
/// returns an empty list when <c>Granit.MultiTenancy</c> is not loaded, so single-tenant
/// deployments still get a host-only result without a hard package dependency.
/// </para>
/// </remarks>
internal sealed class EfUserDirectoryQueryableSource : IUserDirectoryQueryableSource, IDisposable
{
    private readonly DualScopeStorageMode _storageMode;
    private readonly bool _bypassTenantFilter;
    private readonly IDbContextFactory<IdentityHostDbContext> _hostFactory;
    private readonly IDbContextFactory<IdentityTenantDbContext>? _tenantFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantsAccessor _tenantsAccessor;
    private DbContext? _context;
    private List<User>? _materialized;

    public EfUserDirectoryQueryableSource(
        IdentityEntityFrameworkCoreOptions options,
        ICurrentTenant currentTenant,
        ITenantsAccessor tenantsAccessor,
        IDbContextFactory<IdentityHostDbContext> hostFactory,
        IDbContextFactory<IdentityTenantDbContext>? tenantFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(tenantsAccessor);
        ArgumentNullException.ThrowIfNull(hostFactory);

        _storageMode = options.StorageMode;
        _bypassTenantFilter = !currentTenant.IsAvailable;
        _hostFactory = hostFactory;
        _tenantFactory = tenantFactory;
        _currentTenant = currentTenant;
        _tenantsAccessor = tenantsAccessor;
    }

    public IQueryable<User> GetQueryable()
    {
        if (_storageMode == DualScopeStorageMode.Segregated && _bypassTenantFilter)
        {
            _materialized ??= MaterialiseAcrossAllTenants();
            return _materialized.AsQueryable();
        }

        _context ??= OpenContext();

        IQueryable<User> query = ((IIdentityDbContext)_context).Users.AsNoTracking();

        return _bypassTenantFilter
            ? query.IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            : query;
    }

    public void Dispose() => _context?.Dispose();

    private DbContext OpenContext() => _storageMode switch
    {
        DualScopeStorageMode.Shared => _hostFactory.CreateDbContext(),
        DualScopeStorageMode.Segregated => _tenantFactory!.CreateDbContext(),
        _ => throw new InvalidOperationException($"Unknown DualScopeStorageMode: {_storageMode}."),
    };

    private List<User> MaterialiseAcrossAllTenants()
    {
        List<User> results = [];

        using (IdentityHostDbContext host = _hostFactory.CreateDbContext())
        {
            results.AddRange(host.Users
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
            using (IdentityTenantDbContext tenantCtx = _tenantFactory.CreateDbContext())
            {
                results.AddRange(tenantCtx.Users.AsNoTracking().ToList());
            }
        }

        return results;
    }
}

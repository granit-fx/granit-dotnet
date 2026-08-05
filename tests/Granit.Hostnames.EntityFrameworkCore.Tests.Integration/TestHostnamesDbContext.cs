using Granit.DataFiltering;
using Granit.Domain;
using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Minimal host DbContext used by Postgres integration tests — wires the
/// <see cref="ManagedHostname"/> entity configuration via
/// <see cref="HostnamesModelBuilderExtensions.ConfigureHostnamesModule"/>. Deriving
/// <see cref="GranitDbContext"/> applies the Granit conventions (SVO value converters,
/// enum-as-string) — the single supported path since #3162.
/// </summary>
/// <remarks>
/// The suite is tenant-AGNOSTIC: rows carry real tenant ids but there is no ambient
/// tenant (the legacy <c>ApplyGranitConventions()</c> path never wired the tenant
/// filter here). <see cref="TenantFilterDisabledDataFilter"/> switches the
/// parameterised <see cref="IMultiTenant"/> filter off so every row stays visible,
/// preserving the pre-#3162 behavior of this context.
/// </remarks>
internal sealed class TestHostnamesDbContext(DbContextOptions<TestHostnamesDbContext> options)
    : GranitDbContext(options, new NullTenantContext(), TenantFilterDisabledDataFilter.Instance)
{
    public DbSet<ManagedHostname> ManagedHostnames => Set<ManagedHostname>();

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureHostnamesModule();

    private sealed class TenantFilterDisabledDataFilter : IDataFilter
    {
        public static readonly TenantFilterDisabledDataFilter Instance = new();

        public bool IsEnabled<TFilter>() where TFilter : class =>
            typeof(TFilter) != typeof(IMultiTenant);

        public IDisposable Disable<TFilter>() where TFilter : class => NullScope.Instance;

        public IDisposable Enable<TFilter>() where TFilter : class => NullScope.Instance;

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}

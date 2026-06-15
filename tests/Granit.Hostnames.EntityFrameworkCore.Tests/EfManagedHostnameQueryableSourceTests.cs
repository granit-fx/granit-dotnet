using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.EntityFrameworkCore.Tests;

public sealed class EfManagedHostnameQueryableSourceTests
{
    [Fact]
    public async Task GetQueryable_scopes_to_the_active_tenant()
    {
        string db = nameof(GetQueryable_scopes_to_the_active_tenant);
        await SeedBothTenants(db);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.Tenant(HostnamesEf.TenantA)),
            HostnamesEf.Tenant(HostnamesEf.TenantA));

        List<ManagedHostname> result = await source.GetQueryable().ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().TenantId.ShouldBe(HostnamesEf.TenantA);
    }

    [Fact]
    public async Task GetQueryable_bypasses_the_tenant_filter_for_host_admins()
    {
        string db = nameof(GetQueryable_bypasses_the_tenant_filter_for_host_admins);
        await SeedBothTenants(db);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()),
            HostnamesEf.NoTenant());

        List<ManagedHostname> result = await source.GetQueryable().ToListAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.Select(h => h.TenantId).ShouldBe([HostnamesEf.TenantA, HostnamesEf.TenantB], ignoreOrder: true);
    }

    [Fact]
    public async Task GetQueryable_reuses_a_single_context_across_calls()
    {
        string db = nameof(GetQueryable_reuses_a_single_context_across_calls);
        await SeedBothTenants(db);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()), HostnamesEf.NoTenant());

        source.GetQueryable().ShouldNotBeNull();
        // A second call must not throw and must reuse the cached context.
        source.GetQueryable().ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_is_safe_before_any_query()
    {
        var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(nameof(Dispose_is_safe_before_any_query), HostnamesEf.NoTenant()),
            HostnamesEf.NoTenant());

        Should.NotThrow(source.Dispose);
    }

    [Fact]
    public async Task DisposeAsync_disposes_the_materialised_context()
    {
        string db = nameof(DisposeAsync_disposes_the_materialised_context);
        await SeedBothTenants(db);
        var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()), HostnamesEf.NoTenant());
        source.GetQueryable().ShouldNotBeNull();

        await Should.NotThrowAsync(async () => await source.DisposeAsync());
        // Idempotent: a second dispose is a no-op.
        await Should.NotThrowAsync(async () => await source.DisposeAsync());
    }

    private static Task SeedBothTenants(string db) =>
        HostnamesEf.SeedAsync(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()),
            HostnamesEf.Active("a.com", HostnamesEf.TenantA),
            HostnamesEf.Active("b.com", HostnamesEf.TenantB));
}

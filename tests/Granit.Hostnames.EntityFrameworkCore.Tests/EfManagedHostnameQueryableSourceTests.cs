using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
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
        ICurrentTenant tenant = HostnamesEf.Tenant(HostnamesEf.TenantA);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, tenant),
            HostnamesEf.Scope(tenant));

        List<ManagedHostname> result = await source.GetQueryable().ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().TenantId.ShouldBe(HostnamesEf.TenantA);
    }

    [Fact]
    public async Task GetQueryable_bypasses_the_tenant_filter_for_signaled_host_access()
    {
        // VULN-001: cross-tenant visibility requires a signaled .AllowHostAccess() request.
        string db = nameof(GetQueryable_bypasses_the_tenant_filter_for_signaled_host_access);
        await SeedBothTenants(db);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()),
            HostnamesEf.Scope(HostnamesEf.NoTenant(), hostAccess: true));

        List<ManagedHostname> result = await source.GetQueryable().ToListAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.Select(h => h.TenantId).ShouldBe([HostnamesEf.TenantA, HostnamesEf.TenantB], ignoreOrder: true);
    }

    [Fact]
    public async Task GetQueryable_fails_closed_to_host_partition_without_host_signal()
    {
        // VULN-001: an unsignaled absent tenant must NOT leak foreign-tenant rows — only the host
        // partition (TenantId == null) is visible. Both seeded rows carry a tenant, so none show.
        string db = nameof(GetQueryable_fails_closed_to_host_partition_without_host_signal);
        await SeedBothTenants(db);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()),
            HostnamesEf.Scope(HostnamesEf.NoTenant()));

        List<ManagedHostname> result = await source.GetQueryable().ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetQueryable_reuses_a_single_context_across_calls()
    {
        string db = nameof(GetQueryable_reuses_a_single_context_across_calls);
        await SeedBothTenants(db);
        await using var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()), HostnamesEf.Scope(HostnamesEf.NoTenant()));

        source.GetQueryable().ShouldNotBeNull();
        // A second call must not throw and must reuse the cached context.
        source.GetQueryable().ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_is_safe_before_any_query()
    {
        var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(nameof(Dispose_is_safe_before_any_query), HostnamesEf.NoTenant()),
            HostnamesEf.Scope(HostnamesEf.NoTenant()));

        Should.NotThrow(source.Dispose);
    }

    [Fact]
    public async Task DisposeAsync_disposes_the_materialised_context()
    {
        string db = nameof(DisposeAsync_disposes_the_materialised_context);
        await SeedBothTenants(db);
        var source = new EfManagedHostnameQueryableSource(
            HostnamesEf.Factory(db, HostnamesEf.NoTenant()), HostnamesEf.Scope(HostnamesEf.NoTenant()));
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

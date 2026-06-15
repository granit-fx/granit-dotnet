using Granit.Hostnames.Contracts;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.EntityFrameworkCore.Tests;

public sealed class EfHostnameResolverTests
{
    [Fact]
    public async Task ResolveAsync_returns_null_for_an_invalid_host()
    {
        EfHostnameResolver resolver = Create(nameof(ResolveAsync_returns_null_for_an_invalid_host), HostnamesEf.NoTenant());

        ResolvedHostname? result = await resolver.ResolveAsync("not a valid!!host", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_returns_null_when_no_hostname_matches()
    {
        EfHostnameResolver resolver = Create(nameof(ResolveAsync_returns_null_when_no_hostname_matches), HostnamesEf.NoTenant());

        ResolvedHostname? result = await resolver.ResolveAsync("unknown.example", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ignores_hostnames_that_are_not_active()
    {
        string db = nameof(ResolveAsync_ignores_hostnames_that_are_not_active);
        await HostnamesEf.SeedAsync(HostnamesEf.Factory(db, HostnamesEf.NoTenant()), HostnamesEf.Pending("pending.example", HostnamesEf.TenantA));
        EfHostnameResolver resolver = Create(db, HostnamesEf.NoTenant());

        ResolvedHostname? result = await resolver.ResolveAsync("pending.example", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_resolves_an_active_hostname_across_tenants()
    {
        string db = nameof(ResolveAsync_resolves_an_active_hostname_across_tenants);
        // Resolution is tenant-agnostic: a TenantB hostname resolves with no ambient tenant.
        await HostnamesEf.SeedAsync(HostnamesEf.Factory(db, HostnamesEf.NoTenant()), HostnamesEf.Active("acme.com", HostnamesEf.TenantB, isPrimary: true));
        EfHostnameResolver resolver = Create(db, HostnamesEf.NoTenant());

        ResolvedHostname? result = await resolver.ResolveAsync("ACME.COM", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Host.ShouldBe("acme.com");
        result.OwnerType.ShouldBe("cms.site");
        result.TenantId.ShouldBe(HostnamesEf.TenantB);
        result.IsPrimary.ShouldBeTrue();
    }

    private static EfHostnameResolver Create(string db, ICurrentTenant tenant) =>
        new(HostnamesEf.Factory(db, tenant), HostnamesEf.Metrics());
}

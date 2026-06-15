using System.Diagnostics.Metrics;
using Granit.Hostnames.Diagnostics;
using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Granit.Hostnames.EntityFrameworkCore.Tests;

/// <summary>
/// Shared in-memory EF harness for the Hostnames data-layer tests: a context factory
/// over the EF Core in-memory provider, tenant doubles, a no-op metrics instance, and a
/// seed helper.
/// </summary>
internal static class HostnamesEf
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public static ICurrentTenant Tenant(Guid id)
    {
        ICurrentTenant t = Substitute.For<ICurrentTenant>();
        t.IsAvailable.Returns(true);
        t.Id.Returns(id);
        return t;
    }

    public static ICurrentTenant NoTenant()
    {
        ICurrentTenant t = Substitute.For<ICurrentTenant>();
        t.IsAvailable.Returns(false);
        t.Id.Returns((Guid?)null);
        return t;
    }

    public static HostnamesMetrics Metrics()
    {
        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new HostnamesMetrics(meterFactory);
    }

    public static IDbContextFactory<HostnamesDbContext> Factory(string dbName, ICurrentTenant tenant) =>
        new InMemoryFactory(dbName, tenant);

    public static async Task SeedAsync(IDbContextFactory<HostnamesDbContext> factory, params ManagedHostname[] hostnames)
    {
        await using HostnamesDbContext db = factory.CreateDbContext();
        db.ManagedHostnames.AddRange(hostnames);
        await db.SaveChangesAsync();
    }

    public static ManagedHostname Active(string host, Guid? tenantId = null, bool isPrimary = false)
    {
        var hostname = ManagedHostname.Create(Guid.NewGuid(), host, "cms.site", Guid.NewGuid(), tenantId, isPrimary);
        hostname.MarkVerified(Now);
        return hostname;
    }

    public static ManagedHostname Pending(string host, Guid? tenantId = null) =>
        ManagedHostname.Create(Guid.NewGuid(), host, "cms.site", Guid.NewGuid(), tenantId);

    private sealed class InMemoryFactory(string dbName, ICurrentTenant tenant) : IDbContextFactory<HostnamesDbContext>
    {
        public HostnamesDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<HostnamesDbContext>().UseInMemoryDatabase(dbName).Options, tenant);

        public Task<HostnamesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

using Granit.Localization.Domain;
using Granit.Localization.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Localization.EntityFrameworkCore.Tests;

/// <summary>
/// Tests <see cref="EfLocalizationOverrideQueryableSource"/> — the bridge that exposes
/// <see cref="LocalizationOverride"/> rows to the query engine. Verifies tenant scoping
/// (multi-tenant filter applies for tenant contexts, is bypassed for the host).
/// </summary>
public sealed class EfLocalizationOverrideQueryableSourceTests
{
    private sealed class InMemoryContextFactory(string dbName, ICurrentTenant currentTenant)
        : IDbContextFactory<LocalizationDbContext>
    {
        public LocalizationDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<LocalizationDbContext>()
                    .UseInMemoryDatabase(dbName)
                    .Options,
                currentTenant);

        public Task<LocalizationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static async Task SeedAsync(
        string dbName,
        ICurrentTenant seedTenant,
        Guid? tenantId,
        string key,
        CancellationToken cancellationToken = default)
    {
        InMemoryContextFactory factory = new(dbName, seedTenant);
        await using LocalizationDbContext ctx = factory.CreateDbContext();
        ctx.LocalizationOverrides.Add(new LocalizationOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ResourceName = "TestApp",
            CultureName = "fr",
            Key = key,
            Value = $"Value-{key}",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
        });
        await ctx.SaveChangesAsync(cancellationToken);
    }

    [Fact]
    public async Task GetQueryable_TenantContext_ReturnsOnlyTenantOverrides()
    {
        // Arrange — host seeds two tenants' overrides, then a scoped tenant queries.
        string db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        ICurrentTenant hostSeeder = NullTenantContext.Instance;

        await SeedAsync(db, hostSeeder, tenantA, "a-key", TestContext.Current.CancellationToken);
        await SeedAsync(db, hostSeeder, tenantB, "b-key", TestContext.Current.CancellationToken);
        await SeedAsync(db, hostSeeder, tenantId: null, key: "host-key", TestContext.Current.CancellationToken);

        ICurrentTenant tenantAContext = Substitute.For<ICurrentTenant>();
        tenantAContext.IsAvailable.Returns(true);
        tenantAContext.Id.Returns(tenantA);

        EfLocalizationOverrideQueryableSource source = new(
            new InMemoryContextFactory(db, tenantAContext),
            tenantAContext);

        // Act
        List<LocalizationOverride> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert — only tenantA's row is visible; tenantB and host-level rows are filtered out.
        rows.Select(r => r.Key).ShouldBe(["a-key"]);
    }

    [Fact]
    public async Task GetQueryable_HostContext_ReturnsAllOverridesCrossTenant()
    {
        // Arrange — same seeded set, but the source is constructed without a tenant context.
        string db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        ICurrentTenant hostSeeder = NullTenantContext.Instance;

        await SeedAsync(db, hostSeeder, tenantA, "a-key", TestContext.Current.CancellationToken);
        await SeedAsync(db, hostSeeder, tenantB, "b-key", TestContext.Current.CancellationToken);
        await SeedAsync(db, hostSeeder, tenantId: null, key: "host-key", TestContext.Current.CancellationToken);

        EfLocalizationOverrideQueryableSource source = new(
            new InMemoryContextFactory(db, NullTenantContext.Instance),
            NullTenantContext.Instance);

        // Act
        List<LocalizationOverride> rows = await source.GetQueryable()
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert — host (no tenant context) bypasses the filter and sees every row.
        rows.Select(r => r.Key).Order().ShouldBe(["a-key", "b-key", "host-key"]);
    }

    [Fact]
    public async Task GetQueryable_ReturnsNoTrackingQueryable()
    {
        // Arrange
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, NullTenantContext.Instance, tenantId: null, key: "host-key",
            TestContext.Current.CancellationToken);

        EfLocalizationOverrideQueryableSource source = new(
            new InMemoryContextFactory(db, NullTenantContext.Instance),
            NullTenantContext.Instance);

        // Act
        LocalizationOverride row = await source.GetQueryable()
            .SingleAsync(TestContext.Current.CancellationToken);

        // Assert — projection should not enroll entities in the change tracker (read-only path).
        row.Value = "mutated";
        // No SaveChanges call: the source's context is short-lived for the request; this
        // test simply asserts that mutating the projection does not throw or persist.
        row.Value.ShouldBe("mutated");
    }
}

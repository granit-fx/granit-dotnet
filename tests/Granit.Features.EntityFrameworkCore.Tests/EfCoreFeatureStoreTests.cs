using Granit.Events;
using Granit.Features.EntityFrameworkCore.Internal;
using Granit.Features.Events;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class EfCoreFeatureStoreTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private sealed class InMemoryContextFactory(string dbName) : IDbContextFactory<FeaturesDbContext>
    {
        public FeaturesDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<FeaturesDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options);

        public Task<FeaturesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static EfCoreFeatureStore CreateStore(string dbName, ILocalEventBus? eventBus = null) =>
        new(new InMemoryContextFactory(dbName),
            eventBus ?? Substitute.For<ILocalEventBus>(),
            TimeProvider.System);

    private static async Task SeedAsync(
        string dbName,
        Guid tenantId,
        string featureName,
        string value,
        CancellationToken cancellationToken = default)
    {
        InMemoryContextFactory factory = new(dbName);
        await using FeaturesDbContext ctx = factory.CreateDbContext();
        ctx.FeatureOverrides.Add(new TenantFeatureOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FeatureName = featureName,
            Value = value,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
        });
        await ctx.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // GetOrNullAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrNullAsync_ExistingOverride_ReturnsValue()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, tenantId, "Acme.MaxUsersCount", "500",
            TestContext.Current.CancellationToken);

        EfCoreFeatureStore store = CreateStore(db);
        string? result = await store.GetOrNullAsync(
            "Acme.MaxUsersCount", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        result.ShouldBe("500");
    }

    [Fact]
    public async Task GetOrNullAsync_NoOverride_ReturnsNull()
    {
        EfCoreFeatureStore store = CreateStore(Guid.NewGuid().ToString());

        string? result = await store.GetOrNullAsync(
            "Acme.VideoConference", Guid.NewGuid().ToString(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrNullAsync_DifferentTenant_ReturnsNull()
    {
        string db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedAsync(db, tenantA, "Acme.MaxUsersCount", "500",
            TestContext.Current.CancellationToken);

        EfCoreFeatureStore store = CreateStore(db);
        string? result = await store.GetOrNullAsync(
            "Acme.MaxUsersCount", tenantB.ToString(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull("override belongs to tenant A, not tenant B");
    }

    [Fact]
    public async Task GetOrNullAsync_NullTenantId_GlobalScope_ReturnsValue()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreFeatureStore store = CreateStore(db);

        // Store a global (null tenant) override via the store itself
        await store.SetAsync(
            "Acme.GlobalFeature", null, "global-value",
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync(
            "Acme.GlobalFeature", null,
            TestContext.Current.CancellationToken);

        result.ShouldBe("global-value");
    }

    // -------------------------------------------------------------------------
    // SetAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_NewOverride_PersistsValue()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        EfCoreFeatureStore store = CreateStore(db);

        await store.SetAsync(
            "Acme.MaxUsersCount", tenantId.ToString(), "1000",
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync(
            "Acme.MaxUsersCount", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        result.ShouldBe("1000");
    }

    [Fact]
    public async Task SetAsync_ExistingOverride_UpdatesValue()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, tenantId, "Acme.MaxUsersCount", "200",
            TestContext.Current.CancellationToken);

        EfCoreFeatureStore store = CreateStore(db);
        await store.SetAsync(
            "Acme.MaxUsersCount", tenantId.ToString(), "5000",
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync(
            "Acme.MaxUsersCount", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        result.ShouldBe("5000");
    }

    [Fact]
    public async Task SetAsync_IsIdempotent_WhenCalledTwiceWithSameValue()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        EfCoreFeatureStore store = CreateStore(db);

        await store.SetAsync(
            "Acme.Feature", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);
        await store.SetAsync(
            "Acme.Feature", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);

        InMemoryContextFactory factory = new(db);
        await using FeaturesDbContext ctx = factory.CreateDbContext();
        int count = await ctx.FeatureOverrides.CountAsync(
            o => o.FeatureName == "Acme.Feature" && o.TenantId == tenantId,
            TestContext.Current.CancellationToken);

        count.ShouldBe(1, "upsert must not create duplicates");
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ExistingOverride_RemovesRow()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, tenantId, "Acme.VideoConference", "true",
            TestContext.Current.CancellationToken);

        EfCoreFeatureStore store = CreateStore(db);
        await store.DeleteAsync(
            "Acme.VideoConference", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync(
            "Acme.VideoConference", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_UnknownOverride_DoesNotThrow()
    {
        EfCoreFeatureStore store = CreateStore(Guid.NewGuid().ToString());

        Func<Task> act = () => store.DeleteAsync(
            "Acme.Ghost", Guid.NewGuid().ToString(),
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task DeleteAsync_IsolatesTenants_DoesNotRemoveOtherTenantOverride()
    {
        string db = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedAsync(db, tenantA, "Acme.Feature", "true",
            TestContext.Current.CancellationToken);
        await SeedAsync(db, tenantB, "Acme.Feature", "true",
            TestContext.Current.CancellationToken);

        EfCoreFeatureStore store = CreateStore(db);
        await store.DeleteAsync(
            "Acme.Feature", tenantA.ToString(),
            TestContext.Current.CancellationToken);

        string? resultB = await store.GetOrNullAsync(
            "Acme.Feature", tenantB.ToString(),
            TestContext.Current.CancellationToken);

        resultB.ShouldBe("true", "tenant B override must not be affected");
    }

    // -------------------------------------------------------------------------
    // FeatureOverrideChangedEvent emission
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_Publishes_FeatureOverrideChangedEvent()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(db, eventBus);

        await store.SetAsync(
            "Acme.MaxUsers", tenantId.ToString(), "100",
            TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<FeatureOverrideChangedEvent>(e =>
                e.FeatureName == "Acme.MaxUsers" &&
                e.TenantId == tenantId &&
                e.OldValue == null &&
                e.NewValue == "100"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_IncludesOldValue_WhenUpdating()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, tenantId, "Acme.MaxUsers", "50",
            TestContext.Current.CancellationToken);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(db, eventBus);

        await store.SetAsync(
            "Acme.MaxUsers", tenantId.ToString(), "200",
            TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<FeatureOverrideChangedEvent>(e =>
                e.OldValue == "50" &&
                e.NewValue == "200"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Publishes_FeatureOverrideChangedEvent_WithNullNewValue()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        await SeedAsync(db, tenantId, "Acme.Feature", "true",
            TestContext.Current.CancellationToken);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(db, eventBus);

        await store.DeleteAsync(
            "Acme.Feature", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<FeatureOverrideChangedEvent>(e =>
                e.OldValue == "true" &&
                e.NewValue == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_UnknownOverride_DoesNotPublishEvent()
    {
        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(Guid.NewGuid().ToString(), eventBus);

        await store.DeleteAsync(
            "Acme.Ghost", Guid.NewGuid().ToString(),
            TestContext.Current.CancellationToken);

        await eventBus.DidNotReceive().PublishAsync(
            Arg.Any<FeatureOverrideChangedEvent>(),
            Arg.Any<CancellationToken>());
    }
}

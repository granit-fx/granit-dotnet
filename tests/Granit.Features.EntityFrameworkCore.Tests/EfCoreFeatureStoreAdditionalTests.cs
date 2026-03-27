using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Features.Diagnostics;
using Granit.Features.EntityFrameworkCore.Internal;
using Granit.Features.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class EfCoreFeatureStoreAdditionalTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];
        public Meter Create(MeterOptions options) { Meter m = new(options); _meters.Add(m); return m; }
        public void Dispose() { foreach (Meter m in _meters) { m.Dispose(); } }
    }

    private sealed class InMemoryContextFactory(string dbName) : IDbContextFactory<FeaturesDbContext>
    {
        public FeaturesDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<FeaturesDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options);

        public Task<FeaturesDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private static EfCoreFeatureStore CreateStore(
        string dbName,
        ILocalEventBus? eventBus = null,
        TimeProvider? timeProvider = null) =>
        new(new InMemoryContextFactory(dbName),
            eventBus ?? Substitute.For<ILocalEventBus>(),
            timeProvider ?? TimeProvider.System,
            new FeaturesMetrics(new TestMeterFactory()),
            NullLogger<EfCoreFeatureStore>.Instance);

    // -------------------------------------------------------------------------
    // SetAsync — publishes FeatureValueChangedEvent
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_Publishes_FeatureValueChangedEvent()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();
        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(db, eventBus);

        await store.SetAsync(
            "Acme.Feature", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<FeatureValueChangedEvent>(e =>
                e.FeatureName == "Acme.Feature" &&
                e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Publishes_FeatureValueChangedEvent()
    {
        string db = Guid.NewGuid().ToString();
        var tenantId = Guid.NewGuid();

        // Seed an override first
        EfCoreFeatureStore seedStore = CreateStore(db);
        await seedStore.SetAsync("Acme.Feature", tenantId.ToString(), "true",
            TestContext.Current.CancellationToken);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(db, eventBus);

        await store.DeleteAsync(
            "Acme.Feature", tenantId.ToString(),
            TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<FeatureValueChangedEvent>(e =>
                e.FeatureName == "Acme.Feature" &&
                e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_UnknownOverride_DoesNotPublishValueChangedEvent()
    {
        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(Guid.NewGuid().ToString(), eventBus);

        await store.DeleteAsync(
            "Acme.Ghost", Guid.NewGuid().ToString(),
            TestContext.Current.CancellationToken);

        await eventBus.DidNotReceive().PublishAsync(
            Arg.Any<FeatureValueChangedEvent>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SetAsync — global scope (null tenantId)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_NullTenantId_PersistsGlobalOverride()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreFeatureStore store = CreateStore(db);

        await store.SetAsync("Acme.Feature", null, "global-value",
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync("Acme.Feature", null,
            TestContext.Current.CancellationToken);

        result.ShouldBe("global-value");
    }

    [Fact]
    public async Task SetAsync_NullTenantId_UpdatesExistingGlobalOverride()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreFeatureStore store = CreateStore(db);

        await store.SetAsync("Acme.Feature", null, "first",
            TestContext.Current.CancellationToken);
        await store.SetAsync("Acme.Feature", null, "second",
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync("Acme.Feature", null,
            TestContext.Current.CancellationToken);

        result.ShouldBe("second");
    }

    // -------------------------------------------------------------------------
    // DeleteAsync — global scope
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_NullTenantId_RemovesGlobalOverride()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreFeatureStore store = CreateStore(db);

        await store.SetAsync("Acme.Feature", null, "value",
            TestContext.Current.CancellationToken);
        await store.DeleteAsync("Acme.Feature", null,
            TestContext.Current.CancellationToken);

        string? result = await store.GetOrNullAsync("Acme.Feature", null,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // ParseTenantId — invalid GUID returns null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrNullAsync_InvalidGuidTenantId_TreatedAsNull()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreFeatureStore store = CreateStore(db);

        // Store with null tenant (parsed from invalid string)
        await store.SetAsync("Acme.Feature", "not-a-guid", "value",
            TestContext.Current.CancellationToken);

        // Retrieve with null — should match since "not-a-guid" parsed to null
        string? result = await store.GetOrNullAsync("Acme.Feature", null,
            TestContext.Current.CancellationToken);

        result.ShouldBe("value");
    }

    // -------------------------------------------------------------------------
    // TimeProvider usage
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetAsync_UsesTimeProvider_ForTimestamp()
    {
        string db = Guid.NewGuid().ToString();
        DateTimeOffset fixedTime = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(fixedTime);

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();
        EfCoreFeatureStore store = CreateStore(db, eventBus, timeProvider);

        await store.SetAsync("Acme.Feature", null, "value",
            TestContext.Current.CancellationToken);

        await eventBus.Received(1).PublishAsync(
            Arg.Is<FeatureOverrideChangedEvent>(e => e.Timestamp == fixedTime),
            Arg.Any<CancellationToken>());
    }
}

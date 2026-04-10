using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.EntityFrameworkCore.Extensions;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Granit.ReferenceData.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class EfCoreReferenceDataStoreAdditionalTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private sealed class TestEntityConfiguration : ReferenceDataEntityTypeConfiguration<TestEntity>
    {
        public TestEntityConfiguration() : base("ref_test_entities") { }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureReferenceData(new TestEntityConfiguration());
        }
    }

    private static EfCoreReferenceDataStore<TestEntity, TestDbContext> CreateStore(string dbName)
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        ServiceProvider sp = services.BuildServiceProvider();
        IFusionCache cache = new FusionCache(new FusionCacheOptions());
        IOptions<ReferenceDataOptions> options = Microsoft.Extensions.Options.Options.Create(new ReferenceDataOptions());

        return new EfCoreReferenceDataStore<TestEntity, TestDbContext>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            cache,
            options,
            ReferenceDataScope.Global,
            Substitute.For<ICurrentTenant>());
    }

    private static async Task SeedAsync(
        string dbName,
        string code,
        string label,
        bool isActive = true,
        int sortOrder = 0,
        string labelFr = "",
        CancellationToken cancellationToken = default)
    {
        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        await using ServiceProvider sp = services.BuildServiceProvider();
        await using TestDbContext context = sp.GetRequiredService<TestDbContext>();

        context.TestEntities.Add(new TestEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            LabelEn = label,
            LabelFr = labelFr,
            IsActive = isActive,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // GetAllAsync — search term filtering
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_SearchByCode_FiltersCorrectly()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "NL", "Netherlands", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SearchTerm: "BE"),
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items[0].Code.ShouldBe("BE");
    }

    [Fact]
    public async Task GetAllAsync_SearchByLabel_FiltersCorrectly()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SearchTerm: "Fran"),
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items[0].Code.ShouldBe("FR");
    }

    [Fact]
    public async Task GetAllAsync_SearchByFrenchLabel_FiltersCorrectly()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", labelFr: "Belgique", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", labelFr: "France", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SearchTerm: "Belgi"),
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items[0].Code.ShouldBe("BE");
    }

    [Fact]
    public async Task GetAllAsync_SearchNoMatch_ReturnsEmpty()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SearchTerm: "XYZ"),
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // GetAllAsync — HasMore flag
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_HasMore_TrueWhenMorePages()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", sortOrder: 1, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", sortOrder: 2, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "NL", "Netherlands", sortOrder: 3, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(Page: 1, PageSize: 2),
            TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeTrue();
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllAsync_HasMore_FalseWhenLastPage()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", sortOrder: 1, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", sortOrder: 2, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // GetAllAsync — sort by label descending
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_SortByLabelDescending_ReturnsSorted()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "NL", "Netherlands", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SortBy: "Label", Descending: true),
            TestContext.Current.CancellationToken);

        result.Items[0].LabelEn.ShouldBe("Netherlands");
        result.Items[1].LabelEn.ShouldBe("Belgium");
    }

    // -------------------------------------------------------------------------
    // GetAllAsync — default sort descending
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_DefaultSortDescending_ReturnsSortedBySortOrderDesc()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", sortOrder: 1, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", sortOrder: 2, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(Descending: true),
            TestContext.Current.CancellationToken);

        result.Items[0].Code.ShouldBe("FR");
        result.Items[1].Code.ShouldBe("BE");
    }

    // -------------------------------------------------------------------------
    // GetAllAsync — page clamping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_NegativePage_ClampedToOne()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(Page: -1),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // SetActiveAsync — nonexistent code
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetActiveAsync_NonExistentCode_DoesNotThrow()
    {
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(Guid.NewGuid().ToString());

        await Should.NotThrowAsync(
            () => store.SetActiveAsync("ZZ", false, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // CreateAsync — idempotency
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_DuplicateCode_ActiveRecord_IsNoOp()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: true, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);

        // Second create with same Code must not throw
        await Should.NotThrowAsync(() => store.CreateAsync(new TestEntity
        {
            Id = Guid.NewGuid(),
            Code = "BE",
            LabelEn = "Belgium (duplicate)",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        }, TestContext.Current.CancellationToken));

        // Original record is preserved
        TestEntity? entity = await store.GetByCodeAsync("BE", TestContext.Current.CancellationToken);
        entity!.LabelEn.ShouldBe("Belgium");
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_InactiveRecord_IsNoOp()
    {
        // This is the exact scenario seen in production: seeder runs a second time,
        // the existing record has IsActive=false (bypassed by the query filter in
        // GetByCodeAsync), so the seeder calls CreateAsync which must not throw 23505.
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: false, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);

        await Should.NotThrowAsync(() => store.CreateAsync(new TestEntity
        {
            Id = Guid.NewGuid(),
            Code = "BE",
            LabelEn = "Belgium",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        }, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // SetActiveAsync — reactivating an inactive record
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetActiveAsync_InactiveRecord_CanBeReactivated()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: false, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);

        // Should succeed even though the record is filtered out by IsActive=false
        await store.SetActiveAsync("BE", true, TestContext.Current.CancellationToken);

        PagedResult<TestEntity> result = await store.GetAllAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Items.ShouldContain(e => e.Code == "BE" && e.IsActive);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync — invalidates cache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_InvalidatesCache()
    {
        string db = Guid.NewGuid().ToString();
        IFusionCache cache = new FusionCache(new FusionCacheOptions());
        IOptions<ReferenceDataOptions> opts = Microsoft.Extensions.Options.Options.Create(new ReferenceDataOptions());

        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(db));
        ServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = new(scopeFactory, cache, opts, ReferenceDataScope.Global, Substitute.For<ICurrentTenant>());

        // Create entity
        TestEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Code = "BE",
            LabelEn = "Belgium",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        };
        await store.CreateAsync(entity, TestContext.Current.CancellationToken);

        // Populate cache
        TestEntity? cached = await store.GetByCodeAsync("BE", TestContext.Current.CancellationToken);
        cached.ShouldNotBeNull();

        // Update should invalidate cache
        cached!.LabelEn = "Kingdom of Belgium";
        await store.UpdateAsync(cached, TestContext.Current.CancellationToken);

        // Re-fetch — should get updated value
        EfCoreReferenceDataStore<TestEntity, TestDbContext> freshStore = new(scopeFactory, cache, opts, ReferenceDataScope.Global, Substitute.For<ICurrentTenant>());
        TestEntity? updated = await freshStore.GetByCodeAsync("BE", TestContext.Current.CancellationToken);

        updated!.LabelEn.ShouldBe("Kingdom of Belgium");
    }
}

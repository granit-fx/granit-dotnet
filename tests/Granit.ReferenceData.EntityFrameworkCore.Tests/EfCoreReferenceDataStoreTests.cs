using Granit.MultiTenancy;
using Granit.QueryEngine;
using Granit.ReferenceData.Domain;
using Granit.ReferenceData.EntityFrameworkCore;
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

public sealed class EfCoreReferenceDataStoreTests
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
            Substitute.For<ICurrentTenant>(),
            Substitute.For<Granit.Guids.IGuidGenerator>());
    }

    private static async Task SeedAsync(
        string dbName,
        string code,
        string label,
        bool isActive = true,
        int sortOrder = 0,
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
            Activated = isActive,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "seed",
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // GetByCodeAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByCodeAsync_ExistingCode_ReturnsEntity()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        TestEntity? result = await store.GetByCodeAsync("BE", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Code.ShouldBe("BE");
        result.LabelEn.ShouldBe("Belgium");
    }

    [Fact]
    public async Task GetByCodeAsync_NonExistentCode_ReturnsNull()
    {
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(Guid.NewGuid().ToString());

        TestEntity? result = await store.GetByCodeAsync("ZZ", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_CachesResult()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "FR", "France", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);

        // First call — fetches from DB
        TestEntity? first = await store.GetByCodeAsync("FR", TestContext.Current.CancellationToken);
        // Second call — should come from cache (same store instance)
        TestEntity? second = await store.GetByCodeAsync("FR", TestContext.Current.CancellationToken);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        first!.Code.ShouldBe("FR");
        second!.Code.ShouldBe("FR");
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_DefaultQuery_ReturnsActiveOnly()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: true, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "XX", "Inactive", isActive: false, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldAllBe(e => e.Activated);
    }

    [Fact]
    public async Task GetAllAsync_ActiveOnlyFalse_ReturnsAll()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: true, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "XX", "Inactive", isActive: false, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(ActiveOnly: false),
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllAsync_SortByCode_ReturnsSorted()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "FR", "France", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "NL", "Netherlands", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SortBy: "Code"),
            TestContext.Current.CancellationToken);

        result.Items[0].Code.ShouldBe("BE");
        result.Items[1].Code.ShouldBe("FR");
        result.Items[2].Code.ShouldBe("NL");
    }

    [Fact]
    public async Task GetAllAsync_SortByCodeDescending_ReturnsSortedDescending()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "FR", "France", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SortBy: "Code", Descending: true),
            TestContext.Current.CancellationToken);

        result.Items[0].Code.ShouldBe("FR");
        result.Items[1].Code.ShouldBe("BE");
    }

    [Fact]
    public async Task GetAllAsync_SortByLabel_ReturnsSortedByLabel()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "NL", "Netherlands", cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(SortBy: "Label"),
            TestContext.Current.CancellationToken);

        result.Items[0].LabelEn.ShouldBe("Belgium");
        result.Items[1].LabelEn.ShouldBe("Netherlands");
    }

    [Fact]
    public async Task GetAllAsync_DefaultSortBySortOrder_ReturnsSortedBySortOrder()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "NL", "Netherlands", sortOrder: 3, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "BE", "Belgium", sortOrder: 1, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", sortOrder: 2, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Items[0].Code.ShouldBe("BE");
        result.Items[1].Code.ShouldBe("FR");
        result.Items[2].Code.ShouldBe("NL");
    }

    [Fact]
    public async Task GetAllAsync_Pagination_ReturnsCorrectPage()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", sortOrder: 1, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "FR", "France", sortOrder: 2, cancellationToken: TestContext.Current.CancellationToken);
        await SeedAsync(db, "NL", "Netherlands", sortOrder: 3, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(Page: 2, PageSize: 1),
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(3, "TotalCount reflects all matching entries before pagination");
        result.Items.Count.ShouldBe(1);
        result.Items[0].Code.ShouldBe("FR");
    }

    [Fact]
    public async Task GetAllAsync_EmptyStore_ReturnsEmptyResult()
    {
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(Guid.NewGuid().ToString());

        PagedResult<TestEntity> result = await store.GetAllAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsEntity()
    {
        string db = Guid.NewGuid().ToString();
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);

        TestEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Code = "DE",
            LabelEn = "Germany",
            Activated = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        };

        await store.CreateAsync(entity, TestContext.Current.CancellationToken);
        TestEntity? loaded = await store.GetByCodeAsync("DE", TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded!.LabelEn.ShouldBe("Germany");
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ModifiesExistingEntity()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", cancellationToken: TestContext.Current.CancellationToken);

        // Use a new store to simulate a fresh scope
        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        TestEntity? entity = await store.GetByCodeAsync("BE", TestContext.Current.CancellationToken);
        entity.ShouldNotBeNull();

        entity!.LabelEn = "Kingdom of Belgium";
        await store.UpdateAsync(entity, TestContext.Current.CancellationToken);

        // Read again from a fresh store to confirm persistence
        EfCoreReferenceDataStore<TestEntity, TestDbContext> freshStore = CreateStore(db);
        TestEntity? updated = await freshStore.GetByCodeAsync("BE", TestContext.Current.CancellationToken);

        updated!.LabelEn.ShouldBe("Kingdom of Belgium");
    }

    // -------------------------------------------------------------------------
    // SetActiveAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetActiveAsync_DeactivatesEntity()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: true, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        await store.SetActiveAsync("BE", false, TestContext.Current.CancellationToken);

        // Query with ActiveOnly: false to see deactivated entry
        PagedResult<TestEntity> result = await store.GetAllAsync(
            new ReferenceDataQuery(ActiveOnly: false),
            TestContext.Current.CancellationToken);

        result.Items.ShouldContain(e => e.Code == "BE" && !e.Activated);
    }

    [Fact]
    public async Task SetActiveAsync_ReactivatesEntity()
    {
        string db = Guid.NewGuid().ToString();
        await SeedAsync(db, "BE", "Belgium", isActive: false, cancellationToken: TestContext.Current.CancellationToken);

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = CreateStore(db);
        await store.SetActiveAsync("BE", true, TestContext.Current.CancellationToken);

        PagedResult<TestEntity> result = await store.GetAllAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Items.ShouldContain(e => e.Code == "BE" && e.Activated);
    }

    // -------------------------------------------------------------------------
    // Cache invalidation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_InvalidatesCache()
    {
        string db = Guid.NewGuid().ToString();

        // Use shared cache to test invalidation
        IFusionCache cache = new FusionCache(new FusionCacheOptions());
        IOptions<ReferenceDataOptions> opts = Microsoft.Extensions.Options.Options.Create(new ReferenceDataOptions());

        ServiceCollection services = new();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(db));
        ServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        EfCoreReferenceDataStore<TestEntity, TestDbContext> store = new(scopeFactory, cache, opts, ReferenceDataScope.Global, Substitute.For<ICurrentTenant>(), Substitute.For<Granit.Guids.IGuidGenerator>());

        // Populate cache by querying
        await store.GetByCodeAsync("DE", TestContext.Current.CancellationToken);

        // Create should invalidate
        await store.CreateAsync(new TestEntity
        {
            Id = Guid.NewGuid(),
            Code = "DE",
            LabelEn = "Germany",
            Activated = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test",
        }, TestContext.Current.CancellationToken);

        TestEntity? result = await store.GetByCodeAsync("DE", TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result!.LabelEn.ShouldBe("Germany");
    }
}

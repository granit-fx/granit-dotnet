using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres-backed verification of the move re-materialisation invariant locked by
/// T4.1: a deep subtree move under a different parent rewrites every descendant's
/// path and depth in a single SQL UPDATE, and prefix collisions on similarly-named
/// siblings are not accidentally affected.
/// </summary>
public sealed class CategoryServiceMovePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DbContextOptions<TaxonomyDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private CategoryService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();

    public CategoryServiceMovePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using TaxonomyDbContext init = new(_options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE taxonomy_category_assignments, taxonomy_categories RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(_options);

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        TaxonomyMetrics metrics = new(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        ILocalEventBus eventBus = Substitute.For<ILocalEventBus>();

        _sut = new CategoryService(_factory, currentTenant, new SimpleGuidGenerator(), eventBus, metrics);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task MoveAsync_DeepSubtree_ReMaterialisesDescendantsViaSingleSqlUpdate()
    {
        // Build: /a/b/c/d, /a/b/e, and target /x
        Category a = await _sut.CreateAsync("products", null, "a",
            cancellationToken: TestContext.Current.CancellationToken);
        Category b = await _sut.CreateAsync("products", a.Id, "b",
            cancellationToken: TestContext.Current.CancellationToken);
        Category c = await _sut.CreateAsync("products", b.Id, "c",
            cancellationToken: TestContext.Current.CancellationToken);
        Category d = await _sut.CreateAsync("products", c.Id, "d",
            cancellationToken: TestContext.Current.CancellationToken);
        Category e = await _sut.CreateAsync("products", b.Id, "e",
            cancellationToken: TestContext.Current.CancellationToken);
        Category x = await _sut.CreateAsync("products", null, "x",
            cancellationToken: TestContext.Current.CancellationToken);

        await _sut.MoveAsync(b.Id, x.Id, TestContext.Current.CancellationToken);

        Category? reloadedB = await _sut.GetByIdAsync(b.Id, TestContext.Current.CancellationToken);
        reloadedB!.Path.ShouldBe("/x/b");
        reloadedB.Depth.ShouldBe(1);

        Category? reloadedC = await _sut.GetByIdAsync(c.Id, TestContext.Current.CancellationToken);
        reloadedC!.Path.ShouldBe("/x/b/c");
        reloadedC.Depth.ShouldBe(2);

        Category? reloadedD = await _sut.GetByIdAsync(d.Id, TestContext.Current.CancellationToken);
        reloadedD!.Path.ShouldBe("/x/b/c/d");
        reloadedD.Depth.ShouldBe(3);

        Category? reloadedE = await _sut.GetByIdAsync(e.Id, TestContext.Current.CancellationToken);
        reloadedE!.Path.ShouldBe("/x/b/e");
        reloadedE.Depth.ShouldBe(2);
    }

    [Fact]
    public async Task MoveAsync_PrefixCollision_DoesNotAffectSiblingsWithSimilarNames()
    {
        Category a = await _sut.CreateAsync("products", null, "a",
            cancellationToken: TestContext.Current.CancellationToken);
        Category ab = await _sut.CreateAsync("products", null, "ab",
            cancellationToken: TestContext.Current.CancellationToken);
        Category aInner = await _sut.CreateAsync("products", a.Id, "inner",
            cancellationToken: TestContext.Current.CancellationToken);
        Category abInner = await _sut.CreateAsync("products", ab.Id, "inner",
            cancellationToken: TestContext.Current.CancellationToken);
        Category x = await _sut.CreateAsync("products", null, "x",
            cancellationToken: TestContext.Current.CancellationToken);

        await _sut.MoveAsync(a.Id, x.Id, TestContext.Current.CancellationToken);

        Category? reloadedAInner = await _sut.GetByIdAsync(aInner.Id, TestContext.Current.CancellationToken);
        reloadedAInner!.Path.ShouldBe("/x/a/inner");

        Category? reloadedAbInner = await _sut.GetByIdAsync(abInner.Id, TestContext.Current.CancellationToken);
        reloadedAbInner!.Path.ShouldBe("/ab/inner"); // unchanged — prefix boundary
    }

    private sealed class TestDbContextFactory(DbContextOptions<TaxonomyDbContext> options)
        : IDbContextFactory<TaxonomyDbContext>
    {
        public TaxonomyDbContext CreateDbContext() => new(options);

        public Task<TaxonomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TaxonomyDbContext>(new(options));
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}

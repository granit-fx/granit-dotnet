using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Validates the T5.2 acceptance criterion: seed 100 tag assignments, hard-delete
/// the underlying targets via raw SQL (bypassing the lifecycle interceptor), run
/// the sweep, and assert all 100 rows are removed.
/// </summary>
public sealed class OrphanAssignmentSweepPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TargetType = "Granit.Documents.Domain.Document";

    private readonly PostgresFixture _postgres;
    private DbContextOptions<TaxonomyDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _tagService = null!;
    private TagAssignmentService _tagAssignments = null!;
    private TaxonomyMetrics _metrics = null!;
    private static readonly Guid TenantId = Guid.NewGuid();

    public OrphanAssignmentSweepPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using TaxonomyDbContext init = new(_options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE taxonomy_tag_assignments, taxonomy_tags, taxonomy_category_assignments, taxonomy_categories RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(_options);

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        _metrics = new TaxonomyMetrics(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _tagService = new TagService(_factory, currentTenant, new SimpleGuidGenerator(), _metrics);
        _tagAssignments = new TagAssignmentService(_factory, currentTenant, new SimpleGuidGenerator(), clock, _metrics);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task ExecuteAsync_OneHundredOrphans_AllRemoved()
    {
        // Seed 100 distinct (tag, target) assignments — every target is a "phantom"
        // that has never existed in any owning module, so the probe will report
        // ExistsAsync=false for every one.
        Tag tag = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        Guid[] targetIds = [.. Enumerable.Range(0, 100).Select(_ => Guid.NewGuid())];
        foreach (Guid targetId in targetIds)
        {
            await _tagAssignments.AssignAsync(
                tag.Id, TargetType, targetId, Guid.NewGuid(),
                TestContext.Current.CancellationToken);
        }

        await using (TaxonomyDbContext verify = new(_options))
        {
            (await verify.TagAssignments.CountAsync(TestContext.Current.CancellationToken))
                .ShouldBe(100);
        }

        OrphanAssignmentSweepService sut = CreateSut(new GoneProbe());
        int deleted = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(100);
        await using TaxonomyDbContext after = new(_options);
        (await after.TagAssignments.CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    private OrphanAssignmentSweepService CreateSut(ITaggableExistenceProbe probe)
    {
        ServiceCollection services = new();
        services.AddKeyedSingleton(TargetType, probe);
        ServiceProvider provider = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new OrphanAssignmentSweepService(
            _factory,
            scopeFactory,
            _metrics,
            NullLogger<OrphanAssignmentSweepService>.Instance);
    }

    private sealed class GoneProbe : ITaggableExistenceProbe
    {
        public Task<bool> ExistsAsync(Guid? tenantId, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class TestDbContextFactory(DbContextOptions<TaxonomyDbContext> options)
        : IDbContextFactory<TaxonomyDbContext>
    {
        public TaxonomyDbContext CreateDbContext() => new(options);

        public Task<TaxonomyDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaxonomyDbContext(options));
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}

using System.Diagnostics.Metrics;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed tests for <c>TagAssignmentService</c> idempotency under
/// contention — the load-bearing invariant of T2.2: when multiple callers race
/// to assign the same <c>(TenantId, TagId, TargetType, TargetId)</c> triplet,
/// exactly one row exists at the end and every call returns the same id.
/// </summary>
public sealed class TagAssignmentConcurrentPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DbContextOptions<TaxonomyDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _tagService = null!;
    private TaxonomyMetrics _metrics = null!;
    private ICurrentTenant _currentTenant = null!;
    private IClock _clock = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string TargetType = "Granit.Documents.Domain.Document";

    public TagAssignmentConcurrentPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using TaxonomyDbContext init = new(_options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE taxonomy_tag_assignments, taxonomy_tags RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(_options);

        _currentTenant = Substitute.For<ICurrentTenant>();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        _metrics = new TaxonomyMetrics(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTimeOffset.UtcNow);

        _tagService = new TagService(_factory, _currentTenant, new SimpleGuidGenerator(), _metrics);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task AssignAsync_HighConcurrency_ProducesSingleRowAndIdempotentResults()
    {
        Tag tag = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        const int parallelism = 16;
        TagAssignmentService[] services = Enumerable
            .Range(0, parallelism)
            .Select(_ => new TagAssignmentService(_factory, _currentTenant, new SimpleGuidGenerator(), _clock, _metrics))
            .ToArray();

        Task<(TagAssignment Assignment, bool Created)>[] tasks = services
            .Select(s => Task.Run(() => s.AssignAsync(
                tag.Id, TargetType, targetId, userId, TestContext.Current.CancellationToken)))
            .ToArray();

        (TagAssignment Assignment, bool Created)[] results = await Task.WhenAll(tasks);

        // Every call returns the same row id.
        results.Select(r => r.Assignment.Id).Distinct().ShouldHaveSingleItem();

        // Exactly one of them succeeded as "created"; the rest are idempotent reads.
        results.Count(r => r.Created).ShouldBe(1);

        // The DB has exactly one row.
        await using TaxonomyDbContext verify = await _factory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        int rowCount = await verify.TagAssignments
            .CountAsync(a => a.TagId == tag.Id && a.TargetId == targetId,
                TestContext.Current.CancellationToken);
        rowCount.ShouldBe(1);
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

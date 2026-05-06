using System.Diagnostics.Metrics;
using Granit.Documents.Domain;
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

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// End-to-end Postgres test for the T6.1 Documents → Taxonomy wire-up: tag a
/// document, list, untag, and verify each step round-trips through the
/// canonical Taxonomy store with <c>TargetType = Granit.Documents.Domain.Document</c>.
/// </summary>
/// <remarks>
/// The test never materialises a <c>Document</c> row — the assignment surface is
/// polymorphic and only stores the target's <c>(TargetType, TargetId)</c>
/// discriminator. Persisting Documents alongside Taxonomy on the same Postgres
/// container would require running migrations for both modules, which is the
/// host's job, not the framework integration test.
/// </remarks>
public sealed class DocumentTaxonomyIntegrationPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private static readonly string DocumentTargetType = typeof(Document).FullName!;
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly PostgresFixture _postgres;
    private DbContextOptions<TaxonomyDbContext> _taxOptions = null!;
    private TaxonomyTestDbContextFactory _taxFactory = null!;
    private TagService _tagService = null!;
    private TagAssignmentService _tagAssignments = null!;

    public DocumentTaxonomyIntegrationPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        _taxOptions = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using TaxonomyDbContext init = new(_taxOptions);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE taxonomy_tag_assignments, taxonomy_tags, "
            + "taxonomy_category_assignments, taxonomy_categories RESTART IDENTITY CASCADE;");

        _taxFactory = new TaxonomyTestDbContextFactory(_taxOptions);

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        TaxonomyMetrics metrics = new(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _tagService = new TagService(_taxFactory, currentTenant, new SimpleGuidGenerator(), metrics);
        _tagAssignments = new TagAssignmentService(_taxFactory, currentTenant, new SimpleGuidGenerator(), clock, metrics);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task DocumentTaggingFlow_AssignListUnassign_RoundTripsThroughTaxonomy()
    {
        var documentId = Guid.NewGuid();

        Tag urgent = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag review = await _tagService.CreateAsync("documents", "review", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        var actorId = Guid.NewGuid();
        await _tagAssignments.AssignAsync(urgent.Id, DocumentTargetType, documentId, actorId,
            TestContext.Current.CancellationToken);
        await _tagAssignments.AssignAsync(review.Id, DocumentTargetType, documentId, actorId,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Tag> listed = await _tagAssignments.ListForTargetAsync(
            DocumentTargetType, documentId, TestContext.Current.CancellationToken);
        listed.Select(t => t.Name).ShouldBe(["review", "urgent"], ignoreOrder: true);

        // Idempotent re-assign returns Created=false.
        (TagAssignment _, bool created) = await _tagAssignments.AssignAsync(
            urgent.Id, DocumentTargetType, documentId, actorId,
            TestContext.Current.CancellationToken);
        created.ShouldBeFalse();

        bool deleted = await _tagAssignments.UnassignAsync(
            urgent.Id, DocumentTargetType, documentId, TestContext.Current.CancellationToken);
        deleted.ShouldBeTrue();

        IReadOnlyList<Tag> after = await _tagAssignments.ListForTargetAsync(
            DocumentTargetType, documentId, TestContext.Current.CancellationToken);
        after.Select(t => t.Name).ShouldBe(["review"]);

        bool reDelete = await _tagAssignments.UnassignAsync(
            urgent.Id, DocumentTargetType, documentId, TestContext.Current.CancellationToken);
        reDelete.ShouldBeFalse();
    }

    private sealed class TaxonomyTestDbContextFactory(DbContextOptions<TaxonomyDbContext> options)
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

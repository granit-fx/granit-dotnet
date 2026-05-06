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
/// Postgres test for the cross-entity search invariant required by T3.1: the same
/// query <c>q=urgent</c> with <c>scope=*</c> finds hits across two different
/// <c>TargetType</c>s and groups them under their respective discriminator.
/// </summary>
public sealed class TagSearchPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DbContextOptions<TaxonomyDbContext> _options = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _tagService = null!;
    private TagAssignmentService _assignmentService = null!;
    private TagSearchService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string DocumentType = "Granit.Documents.Domain.Document";
    private const string PartyType = "Granit.Parties.Domain.Party";

    public TagSearchPostgresTests(PostgresFixture postgres) => _postgres = postgres;

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

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        TaxonomyMetrics metrics = new(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _tagService = new TagService(_factory, currentTenant, new SimpleGuidGenerator(), metrics);
        _assignmentService = new TagAssignmentService(_factory, currentTenant, new SimpleGuidGenerator(), clock, metrics);
        _sut = new TagSearchService(_factory, currentTenant);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task SearchAsync_CrossScope_FindsHitsAcrossTwoTargetTypes()
    {
        Tag urgentDoc = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag urgentParty = await _tagService.CreateAsync("parties", "urgentVip", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        var doc = Guid.NewGuid();
        var party = Guid.NewGuid();
        await _assignmentService.AssignAsync(urgentDoc.Id, DocumentType, doc, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _assignmentService.AssignAsync(urgentParty.Id, PartyType, party, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        TagSearchResult result = await _sut.SearchAsync("urg", "*",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Tags.Count.ShouldBe(2);
        result.HitsByTargetType.Count.ShouldBe(2);
        result.HitsByTargetType[DocumentType].ShouldHaveSingleItem().TargetId.ShouldBe(doc);
        result.HitsByTargetType[PartyType].ShouldHaveSingleItem().TargetId.ShouldBe(party);
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

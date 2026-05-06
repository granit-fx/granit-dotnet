using System.Diagnostics.Metrics;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Taxonomy.Diagnostics;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Internal;

public sealed class TagSearchServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _tagService = null!;
    private TagAssignmentService _assignmentService = null!;
    private TagSearchService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string DocumentType = "Granit.Documents.Domain.Document";
    private const string PartyType = "Granit.Parties.Domain.Party";

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            $"DataSource=file:taxonomy-search-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _holdOpen = new SqliteConnection(connectionString);
        await _holdOpen.OpenAsync();

        DbContextOptions<TaxonomyDbContext> options = new DbContextOptionsBuilder<TaxonomyDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using TaxonomyDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestDbContextFactory(options);

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

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    [Fact]
    public async Task SearchAsync_NoMatchingTags_ReturnsEmptyResult()
    {
        TagSearchResult result = await _sut.SearchAsync("xyz", "*",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Tags.ShouldBeEmpty();
        result.HitsByTargetType.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task SearchAsync_PrefixMatch_ReturnsTagsWithoutAssignmentsWhenNoneExist()
    {
        await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        await _tagService.CreateAsync("parties", "urgentVip", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);
        await _tagService.CreateAsync("documents", "review", "#0000FF",
            cancellationToken: TestContext.Current.CancellationToken);

        TagSearchResult result = await _sut.SearchAsync("urg", "*",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Tags.Count.ShouldBe(2); // urgent + urgentVip
        result.HitsByTargetType.ShouldBeEmpty(); // no assignments
    }

    [Fact]
    public async Task SearchAsync_ScopeFilter_RestrictsToScope()
    {
        await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        await _tagService.CreateAsync("parties", "urgentVip", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        TagSearchResult result = await _sut.SearchAsync("urg", "documents",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Tags.Count.ShouldBe(1);
        result.Tags[0].Name.ShouldBe("urgent");
    }

    [Fact]
    public async Task SearchAsync_CrossTargetType_GroupsHitsByTargetType()
    {
        Tag urgent = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);

        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();
        var party1 = Guid.NewGuid();

        await _assignmentService.AssignAsync(urgent.Id, DocumentType, doc1, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _assignmentService.AssignAsync(urgent.Id, DocumentType, doc2, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _assignmentService.AssignAsync(urgent.Id, PartyType, party1, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        TagSearchResult result = await _sut.SearchAsync("urgent", "*",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Tags.Count.ShouldBe(1);
        result.HitsByTargetType.Count.ShouldBe(2);
        result.HitsByTargetType[DocumentType].Count.ShouldBe(2);
        result.HitsByTargetType[DocumentType].Select(h => h.TargetId).OrderBy(g => g)
            .ShouldBe(new[] { doc1, doc2 }.OrderBy(g => g));
        result.HitsByTargetType[PartyType].Count.ShouldBe(1);
        result.HitsByTargetType[PartyType][0].TargetId.ShouldBe(party1);
    }

    [Fact]
    public async Task SearchAsync_MultipleTagsOnSameTarget_TagIdsAggregated()
    {
        Tag urgent = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag urgent2 = await _tagService.CreateAsync("documents", "urgentBis", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        var doc = Guid.NewGuid();
        await _assignmentService.AssignAsync(urgent.Id, DocumentType, doc, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _assignmentService.AssignAsync(urgent2.Id, DocumentType, doc, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        TagSearchResult result = await _sut.SearchAsync("urgent", "*",
            cancellationToken: TestContext.Current.CancellationToken);

        result.HitsByTargetType[DocumentType].Count.ShouldBe(1);
        result.HitsByTargetType[DocumentType][0].TargetId.ShouldBe(doc);
        result.HitsByTargetType[DocumentType][0].TagIds.OrderBy(g => g)
            .ShouldBe(new[] { urgent.Id, urgent2.Id }.OrderBy(g => g));
    }

    [Fact]
    public async Task SearchAsync_Pagination_AppliedToTagsOnly()
    {
        for (int i = 0; i < 5; i++)
        {
            await _tagService.CreateAsync("documents", $"urgent-{i}", "#FF0000",
                cancellationToken: TestContext.Current.CancellationToken);
        }

        TagSearchResult result = await _sut.SearchAsync("urgent", "*", skip: 1, take: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Tags.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(5);
        result.Skip.ShouldBe(1);
        result.Take.ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchAsync_BlankQuery_Throws(string query) =>
        await Should.ThrowAsync<ArgumentException>(() =>
            _sut.SearchAsync(query, "*", cancellationToken: TestContext.Current.CancellationToken));

    [Fact]
    public async Task SearchAsync_InvalidTake_Throws() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            _sut.SearchAsync("x", "*", take: 0,
                cancellationToken: TestContext.Current.CancellationToken));

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

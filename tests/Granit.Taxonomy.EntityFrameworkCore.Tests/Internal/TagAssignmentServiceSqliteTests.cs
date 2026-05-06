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

public sealed class TagAssignmentServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _tagService = null!;
    private TagAssignmentService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string TargetType = "Granit.Documents.Domain.Document";

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            $"DataSource=file:taxonomy-{Guid.NewGuid():N}?mode=memory&cache=shared";
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
        _sut = new TagAssignmentService(_factory, currentTenant, new SimpleGuidGenerator(), clock, metrics);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    [Fact]
    public async Task AssignAsync_FirstCall_CreatesRow()
    {
        Tag tag = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();

        (TagAssignment assignment, bool created) = await _sut.AssignAsync(
            tag.Id, TargetType, targetId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        created.ShouldBeTrue();
        assignment.TagId.ShouldBe(tag.Id);
        assignment.TargetType.ShouldBe(TargetType);
        assignment.TargetId.ShouldBe(targetId);
    }

    [Fact]
    public async Task AssignAsync_SameTriplet_Idempotent()
    {
        Tag tag = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        (TagAssignment first, bool firstCreated) = await _sut.AssignAsync(
            tag.Id, TargetType, targetId, userId, TestContext.Current.CancellationToken);
        (TagAssignment second, bool secondCreated) = await _sut.AssignAsync(
            tag.Id, TargetType, targetId, userId, TestContext.Current.CancellationToken);

        firstCreated.ShouldBeTrue();
        secondCreated.ShouldBeFalse();
        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task UnassignAsync_RemovesRow()
    {
        Tag tag = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();
        await _sut.AssignAsync(tag.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        bool deleted = await _sut.UnassignAsync(tag.Id, TargetType, targetId,
            TestContext.Current.CancellationToken);

        deleted.ShouldBeTrue();
        IReadOnlyList<Tag> remaining = await _sut.ListForTargetAsync(TargetType, targetId,
            TestContext.Current.CancellationToken);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnassignAsync_UnknownTriplet_ReturnsFalse()
    {
        bool deleted = await _sut.UnassignAsync(Guid.NewGuid(), TargetType, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        deleted.ShouldBeFalse();
    }

    [Fact]
    public async Task ListForTargetAsync_ReturnsAssignedTags_OrderedByName()
    {
        Tag urgent = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag review = await _tagService.CreateAsync("documents", "review", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);
        await _tagService.CreateAsync("documents", "archived", "#0000FF",
            cancellationToken: TestContext.Current.CancellationToken);

        var targetId = Guid.NewGuid();
        await _sut.AssignAsync(urgent.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _sut.AssignAsync(review.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        IReadOnlyList<Tag> tags = await _sut.ListForTargetAsync(TargetType, targetId,
            TestContext.Current.CancellationToken);

        tags.Count.ShouldBe(2);
        tags.Select(t => t.Name).ShouldBe(["review", "urgent"]);
    }

    [Fact]
    public async Task RemoveAllAssignmentsAsync_RemovesEveryRowForTarget()
    {
        Tag tag1 = await _tagService.CreateAsync("documents", "t1", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag tag2 = await _tagService.CreateAsync("documents", "t2", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);

        var targetId = Guid.NewGuid();
        await _sut.AssignAsync(tag1.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _sut.AssignAsync(tag2.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        int removed = await _sut.RemoveAllAssignmentsAsync(TargetType, targetId,
            TestContext.Current.CancellationToken);

        removed.ShouldBe(2);
        IReadOnlyList<Tag> remaining = await _sut.ListForTargetAsync(TargetType, targetId,
            TestContext.Current.CancellationToken);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAllAssignmentsAsync_UnknownTarget_ReturnsZero()
    {
        int removed = await _sut.RemoveAllAssignmentsAsync(TargetType, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        removed.ShouldBe(0);
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

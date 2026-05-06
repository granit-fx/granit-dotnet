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
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.EntityFrameworkCore.Tests.Internal;

public sealed class OrphanAssignmentSweepServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private TagService _tagService = null!;
    private TagAssignmentService _tagAssignments = null!;
    private CategoryService _categoryService = null!;
    private CategoryAssignmentService _categoryAssignments = null!;
    private TaxonomyMetrics _metrics = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string TargetType = "Granit.Documents.Domain.Document";
    private const string OtherTargetType = "Granit.Parties.Domain.Party";

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
        _metrics = new TaxonomyMetrics(
            services.BuildServiceProvider().GetRequiredService<IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);
        Granit.Events.ILocalEventBus bus = Substitute.For<Granit.Events.ILocalEventBus>();

        _tagService = new TagService(_factory, currentTenant, new SimpleGuidGenerator(), _metrics);
        _tagAssignments = new TagAssignmentService(_factory, currentTenant, new SimpleGuidGenerator(), clock, _metrics);
        _categoryService = new CategoryService(_factory, currentTenant, new SimpleGuidGenerator(), bus, _metrics);
        _categoryAssignments = new CategoryAssignmentService(_factory, currentTenant, new SimpleGuidGenerator(), clock, bus, _metrics);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    [Fact]
    public async Task ExecuteAsync_NoAssignments_ReturnsZero()
    {
        OrphanAssignmentSweepService sut = CreateSut(new StubProbe(_ => false));

        int deleted = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteAsync_TargetStillExists_KeepsAssignments()
    {
        Guid targetId = await SeedTagAssignment();
        OrphanAssignmentSweepService sut = CreateSut(new StubProbe(_ => true));

        int deleted = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
        IReadOnlyList<Tag> remaining = await _tagAssignments.ListForTargetAsync(
            TargetType, targetId, TestContext.Current.CancellationToken);
        remaining.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_TargetGone_DeletesBothTagAndCategoryAssignments()
    {
        var targetId = Guid.NewGuid();
        Tag tag1 = await _tagService.CreateAsync("documents", "urgent", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        Tag tag2 = await _tagService.CreateAsync("documents", "review", "#00FF00",
            cancellationToken: TestContext.Current.CancellationToken);
        await _tagAssignments.AssignAsync(tag1.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await _tagAssignments.AssignAsync(tag2.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Category category = await _categoryService.CreateAsync("documents", null, "tax",
            cancellationToken: TestContext.Current.CancellationToken);
        await _categoryAssignments.AssignAsync(category.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        OrphanAssignmentSweepService sut = CreateSut(new StubProbe(_ => false));
        int deleted = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // 2 tag rows + 1 category row.
        deleted.ShouldBe(3);
        IReadOnlyList<Tag> remaining = await _tagAssignments.ListForTargetAsync(
            TargetType, targetId, TestContext.Current.CancellationToken);
        remaining.Count.ShouldBe(0);
        Category? cat = await _categoryAssignments.GetForTargetAsync(
            TargetType, targetId, TestContext.Current.CancellationToken);
        cat.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_UnregisteredTargetType_KeepsAssignments()
    {
        // No probe registered for the target type → AlwaysExistsProbe wins → no deletion.
        Guid targetId = await SeedTagAssignment();
        OrphanAssignmentSweepService sut = CreateSut(probe: null);

        int deleted = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
        IReadOnlyList<Tag> remaining = await _tagAssignments.ListForTargetAsync(
            TargetType, targetId, TestContext.Current.CancellationToken);
        remaining.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_ProbeThrows_TreatsTargetAsAliveAndContinues()
    {
        Guid throwingId = await SeedTagAssignment();
        Guid orphanId = await SeedTagAssignment();

        StubProbe probe = new(id => id == orphanId
            ? false
            : throw new InvalidOperationException("probe down"));

        OrphanAssignmentSweepService sut = CreateSut(probe);
        int deleted = await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Throwing id is preserved, orphan id is cleaned up.
        deleted.ShouldBe(1);
        (await _tagAssignments.ListForTargetAsync(TargetType, throwingId, TestContext.Current.CancellationToken))
            .Count.ShouldBe(1);
        (await _tagAssignments.ListForTargetAsync(TargetType, orphanId, TestContext.Current.CancellationToken))
            .Count.ShouldBe(0);
    }

    private async Task<Guid> SeedTagAssignment()
    {
        Tag tag = await _tagService.CreateAsync("documents", $"t-{Guid.NewGuid():N}", "#FF0000",
            cancellationToken: TestContext.Current.CancellationToken);
        var targetId = Guid.NewGuid();
        await _tagAssignments.AssignAsync(tag.Id, TargetType, targetId, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        return targetId;
    }

    private OrphanAssignmentSweepService CreateSut(ITaggableExistenceProbe? probe)
    {
        ServiceCollection services = new();
        if (probe is not null)
        {
            services.AddKeyedSingleton(TargetType, probe);
            services.AddKeyedSingleton(OtherTargetType, probe);
        }
        ServiceProvider provider = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new OrphanAssignmentSweepService(
            _factory,
            scopeFactory,
            _metrics,
            NullLogger<OrphanAssignmentSweepService>.Instance);
    }

    private sealed class StubProbe(Func<Guid, bool> exists) : ITaggableExistenceProbe
    {
        public Task<bool> ExistsAsync(Guid? tenantId, Guid targetId, CancellationToken cancellationToken) =>
            Task.FromResult(exists(targetId));
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

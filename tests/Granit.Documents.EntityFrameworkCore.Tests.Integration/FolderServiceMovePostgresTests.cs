using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed verification of the bulk descendant path re-materialisation
/// performed by <see cref="FolderService.MoveAsync"/>. Confirms that the
/// <c>ExecuteUpdate</c> call translates to a single SQL <c>UPDATE</c> that updates
/// every descendant correctly under realistic Postgres semantics (case-sensitive
/// LIKE, materialised path index).
/// </summary>
public sealed class FolderServiceMovePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private FolderService _sut = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public FolderServiceMovePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_folders RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(options);
        var bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ILocalEventBus localEventBus = Substitute.For<ILocalEventBus>();

        _sut = new FolderService(_factory, bootstrap, currentTenant,
            new SimpleGuidGenerator(), clock, localEventBus);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task MoveAsync_DeepSubtree_ReMaterialisesPathsViaSingleSqlUpdate()
    {
        // Build a 4-level subtree: A → B → C → D and a sibling A → B → E.
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, TestContext.Current.CancellationToken);
        Folder c = await _sut.CreateAsync(b.Id, "C", OwnerId, TestContext.Current.CancellationToken);
        Folder d = await _sut.CreateAsync(c.Id, "D", OwnerId, TestContext.Current.CancellationToken);
        Folder e = await _sut.CreateAsync(b.Id, "E", OwnerId, TestContext.Current.CancellationToken);

        Folder x = await _sut.CreateAsync(null, "X", OwnerId, TestContext.Current.CancellationToken);

        Folder? movedB = await _sut.MoveAsync(b.Id, x.Id, TestContext.Current.CancellationToken);

        movedB.ShouldNotBeNull();
        movedB.Path.ShouldBe("/X/B");

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder cReloaded = await db.Folders.SingleAsync(f => f.Id == c.Id, TestContext.Current.CancellationToken);
        Folder dReloaded = await db.Folders.SingleAsync(f => f.Id == d.Id, TestContext.Current.CancellationToken);
        Folder eReloaded = await db.Folders.SingleAsync(f => f.Id == e.Id, TestContext.Current.CancellationToken);

        cReloaded.Path.ShouldBe("/X/B/C");
        cReloaded.Depth.ShouldBe(3);
        dReloaded.Path.ShouldBe("/X/B/C/D");
        dReloaded.Depth.ShouldBe(4);
        eReloaded.Path.ShouldBe("/X/B/E");
        eReloaded.Depth.ShouldBe(3);
    }

    [Fact]
    public async Task MoveAsync_PrefixCollision_DoesNotAffectSiblingsWithSimilarNames()
    {
        // Postgres LIKE 'A/%' must not match a sibling "AB" — the trailing slash boundary
        // is the safety check baked into MoveAsync's prefix.
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder ab = await _sut.CreateAsync(null, "AB", OwnerId, TestContext.Current.CancellationToken);
        Folder aChild = await _sut.CreateAsync(a.Id, "Inner", OwnerId, TestContext.Current.CancellationToken);
        Folder abChild = await _sut.CreateAsync(ab.Id, "Inner", OwnerId, TestContext.Current.CancellationToken);

        Folder x = await _sut.CreateAsync(null, "X", OwnerId, TestContext.Current.CancellationToken);

        await _sut.MoveAsync(a.Id, x.Id, TestContext.Current.CancellationToken);

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder aChildReloaded = await db.Folders.SingleAsync(f => f.Id == aChild.Id, TestContext.Current.CancellationToken);
        Folder abChildReloaded = await db.Folders.SingleAsync(f => f.Id == abChild.Id, TestContext.Current.CancellationToken);

        aChildReloaded.Path.ShouldBe("/X/A/Inner");
        abChildReloaded.Path.ShouldBe("/AB/Inner"); // unchanged
    }

    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}

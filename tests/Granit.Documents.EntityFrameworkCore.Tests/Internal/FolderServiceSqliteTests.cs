using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// SQLite-backed integration tests for <see cref="FolderService"/>. Exercise the same
/// CRUD + breadcrumb flow that the HTTP endpoints (F2.3) wire up, without needing a
/// WebApplicationFactory.
/// </summary>
public sealed class FolderServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private DocumentBootstrapService _bootstrap = null!;
    private FolderService _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            $"DataSource=file:f23-folder-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _holdOpen = new SqliteConnection(connectionString);
        await _holdOpen.OpenAsync();

        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestDbContextFactory(options);
        _bootstrap = new DocumentBootstrapService(_factory, new SequentialGuidGenerator());

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _sut = new FolderService(_factory, _bootstrap, currentTenant, new SequentialGuidGenerator(), clock);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    [Fact]
    public async Task CreateAsync_NullParent_BootstrapsTenantRoot_AndCreatesUnderIt()
    {
        Folder folder = await _sut.CreateAsync(parentFolderId: null, name: "Contracts", OwnerId,
            TestContext.Current.CancellationToken);

        folder.IsTenantRoot.ShouldBeFalse();
        folder.Path.ShouldBe("/Contracts");
        folder.Depth.ShouldBe(1);

        // Tenant root was created lazily.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Folders.CountAsync(f => f.IsTenantRoot, TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task ListChildrenAsync_NullParent_ReturnsRootChildren_ExcludingRootItself()
    {
        await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        await _sut.CreateAsync(null, "B", OwnerId, TestContext.Current.CancellationToken);

        IReadOnlyList<Folder> children = await _sut.ListChildrenAsync(null,
            TestContext.Current.CancellationToken);

        children.Count.ShouldBe(2);
        children.ShouldAllBe(f => !f.IsTenantRoot);
    }

    [Fact]
    public async Task ListChildrenAsync_TrashedChildren_AreExcluded()
    {
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        await _sut.CreateAsync(null, "B", OwnerId, TestContext.Current.CancellationToken);
        await _sut.TrashAsync(a.Id, TestContext.Current.CancellationToken);

        IReadOnlyList<Folder> children = await _sut.ListChildrenAsync(null,
            TestContext.Current.CancellationToken);

        children.Count.ShouldBe(1);
        children[0].Name.ShouldBe("B");
    }

    [Fact]
    public async Task GetBreadcrumbAsync_ReturnsAncestorChain_ExcludingRoot()
    {
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, TestContext.Current.CancellationToken);
        Folder c = await _sut.CreateAsync(b.Id, "C", OwnerId, TestContext.Current.CancellationToken);

        IReadOnlyList<Folder> breadcrumb = await _sut.GetBreadcrumbAsync(c.Id,
            TestContext.Current.CancellationToken);

        breadcrumb.Select(f => f.Name).ShouldBe(["A", "B", "C"]);
    }

    [Fact]
    public async Task GetBreadcrumbAsync_TenantRoot_ReturnsEmpty()
    {
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId,
            TestContext.Current.CancellationToken);

        IReadOnlyList<Folder> breadcrumb = await _sut.GetBreadcrumbAsync(rootId,
            TestContext.Current.CancellationToken);

        breadcrumb.ShouldBeEmpty();
    }

    [Fact]
    public async Task RenameAsync_UpdatesNameAndPath()
    {
        Folder folder = await _sut.CreateAsync(null, "Old", OwnerId,
            TestContext.Current.CancellationToken);

        Folder? renamed = await _sut.RenameAsync(folder.Id, "New",
            TestContext.Current.CancellationToken);

        renamed.ShouldNotBeNull();
        renamed.Name.ShouldBe("New");
        renamed.Path.ShouldBe("/New");
    }

    [Fact]
    public async Task RenameAsync_UnknownId_ReturnsNull()
    {
        Folder? result = await _sut.RenameAsync(Guid.NewGuid(), "X",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TrashAsync_UpdatesStatusAndTrashedAt()
    {
        Folder folder = await _sut.CreateAsync(null, "ToTrash", OwnerId,
            TestContext.Current.CancellationToken);

        Folder? trashed = await _sut.TrashAsync(folder.Id, TestContext.Current.CancellationToken);

        trashed.ShouldNotBeNull();
        trashed.Status.ShouldBe(FolderStatus.Trashed);
        trashed.TrashedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_TenantRoot_IsNotHidden_ButEndpointFiltersIt()
    {
        // The service returns the tenant root when explicitly asked — endpoint logic
        // (F2.3 GET /folders/{id}) is responsible for hiding it from clients.
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId,
            TestContext.Current.CancellationToken);

        Folder? root = await _sut.GetByIdAsync(rootId, TestContext.Current.CancellationToken);

        root.ShouldNotBeNull();
        root.IsTenantRoot.ShouldBeTrue();
    }

    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }

    private sealed class SequentialGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }
}

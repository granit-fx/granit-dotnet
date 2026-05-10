using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Events;
using Granit.Events;
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
    private CapturingLocalEventBus _localEventBus = null!;

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

        _localEventBus = new CapturingLocalEventBus();

        _sut = new FolderService(
            _factory, _bootstrap, currentTenant, new SequentialGuidGenerator(), clock, _localEventBus);
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
            cancellationToken: TestContext.Current.CancellationToken);

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
            cancellationToken: TestContext.Current.CancellationToken);

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
    public async Task TrashAsync_CascadesTo_DescendantFoldersAndDocuments()
    {
        // /A → /A/B → /A/B/C plus a document in /A/B and /A/B/C.
        CancellationToken ct = TestContext.Current.CancellationToken;
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, ct);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, ct);
        Folder c = await _sut.CreateAsync(b.Id, "C", OwnerId, ct);

        await using DocumentsDbContext seed = await _factory.CreateDbContextAsync(ct);
        Folder bAttached = await seed.Folders.SingleAsync(f => f.Id == b.Id, ct);
        Folder cAttached = await seed.Folders.SingleAsync(f => f.Id == c.Id, ct);
        var docInB = Document.Create(Guid.NewGuid(), bAttached, OwnerId, "in-b.pdf");
        var docInC = Document.Create(Guid.NewGuid(), cAttached, OwnerId, "in-c.pdf");
        seed.Documents.Add(docInB);
        seed.Documents.Add(docInC);
        await seed.SaveChangesAsync(ct);

        await _sut.TrashAsync(a.Id, ct);

        await using DocumentsDbContext check = await _factory.CreateDbContextAsync(ct);
        Folder reloadedA = await check.Folders.SingleAsync(f => f.Id == a.Id, ct);
        Folder reloadedB = await check.Folders.SingleAsync(f => f.Id == b.Id, ct);
        Folder reloadedC = await check.Folders.SingleAsync(f => f.Id == c.Id, ct);
        Document reloadedDocB = await check.Documents.SingleAsync(d => d.Id == docInB.Id, ct);
        Document reloadedDocC = await check.Documents.SingleAsync(d => d.Id == docInC.Id, ct);

        reloadedA.Status.ShouldBe(FolderStatus.Trashed);
        reloadedB.Status.ShouldBe(FolderStatus.Trashed);
        reloadedC.Status.ShouldBe(FolderStatus.Trashed);
        reloadedDocB.Status.ShouldBe(DocumentStatus.Trashed);
        reloadedDocC.Status.ShouldBe(DocumentStatus.Trashed);

        // Sibling folders untouched.
        reloadedA.TrashedAt.ShouldBe(reloadedB.TrashedAt);
        reloadedA.TrashedAt.ShouldBe(reloadedC.TrashedAt);
    }

    [Fact]
    public async Task RestoreAsync_TrashedFolder_ReturnsActive_DoesNotCascade()
    {
        // Trashing /A cascades; restoring /A leaves /A/B trashed (per F8.1 — opt-in).
        CancellationToken ct = TestContext.Current.CancellationToken;
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, ct);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, ct);
        await _sut.TrashAsync(a.Id, ct);

        Folder? restored = await _sut.RestoreAsync(a.Id, ct);
        restored.ShouldNotBeNull();
        restored.Status.ShouldBe(FolderStatus.Active);
        restored.TrashedAt.ShouldBeNull();

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(ct);
        Folder reloadedB = await db.Folders.SingleAsync(f => f.Id == b.Id, ct);
        reloadedB.Status.ShouldBe(FolderStatus.Trashed);
    }

    [Fact]
    public async Task RestoreAsync_ParentTrashed_Throws()
    {
        // Direct-restore of a child whose parent is still trashed must fail with
        // InvalidOperationException — caller restores the parent first.
        CancellationToken ct = TestContext.Current.CancellationToken;
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, ct);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, ct);
        await _sut.TrashAsync(a.Id, ct);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _sut.RestoreAsync(b.Id, ct));
    }

    [Fact]
    public async Task RestoreAsync_NotTrashed_ReturnsNull()
    {
        Folder folder = await _sut.CreateAsync(null, "Active", OwnerId,
            TestContext.Current.CancellationToken);

        Folder? result = await _sut.RestoreAsync(folder.Id, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListChildrenAsync_StatusTrashed_ReturnsOnlyTrashedRows()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, ct);
        await _sut.CreateAsync(null, "B", OwnerId, ct);
        await _sut.TrashAsync(a.Id, ct);

        IReadOnlyList<Folder> trashed = await _sut.ListChildrenAsync(
            null, FolderStatus.Trashed, ct);
        trashed.Count.ShouldBe(1);
        trashed[0].Id.ShouldBe(a.Id);
    }

    // -------------------------------------------------------------------------
    // F2.4 — MoveAsync + descendant path re-materialisation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MoveAsync_ToDifferentParent_UpdatesMovedFolderPathAndDepth()
    {
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder b = await _sut.CreateAsync(null, "B", OwnerId, TestContext.Current.CancellationToken);
        Folder leaf = await _sut.CreateAsync(a.Id, "Leaf", OwnerId, TestContext.Current.CancellationToken);

        Folder? moved = await _sut.MoveAsync(leaf.Id, b.Id, TestContext.Current.CancellationToken);

        moved.ShouldNotBeNull();
        moved.ParentFolderId.ShouldBe(b.Id);
        moved.Path.ShouldBe("/B/Leaf");
        moved.Depth.ShouldBe(2);
    }

    [Fact]
    public async Task MoveAsync_ThreeLevelSubtree_ReMaterialisesEveryDescendantPath()
    {
        // Build a 3-level subtree under "A": A/B/C/D and a parallel A/B/C/E plus
        // an unrelated sibling "Other" that must not be touched.
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, TestContext.Current.CancellationToken);
        Folder c = await _sut.CreateAsync(b.Id, "C", OwnerId, TestContext.Current.CancellationToken);
        Folder d = await _sut.CreateAsync(c.Id, "D", OwnerId, TestContext.Current.CancellationToken);
        Folder e = await _sut.CreateAsync(c.Id, "E", OwnerId, TestContext.Current.CancellationToken);

        Folder x = await _sut.CreateAsync(null, "X", OwnerId, TestContext.Current.CancellationToken);
        Folder other = await _sut.CreateAsync(null, "Other", OwnerId, TestContext.Current.CancellationToken);

        // Move B (and its subtree) under X — expected: /X/B, /X/B/C, /X/B/C/D, /X/B/C/E
        Folder? movedB = await _sut.MoveAsync(b.Id, x.Id, TestContext.Current.CancellationToken);

        movedB.ShouldNotBeNull();
        movedB.Path.ShouldBe("/X/B");
        movedB.Depth.ShouldBe(2);

        // Reload descendants from a fresh context to verify the bulk SQL UPDATE landed.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder cReloaded = await db.Folders.SingleAsync(f => f.Id == c.Id, TestContext.Current.CancellationToken);
        Folder dReloaded = await db.Folders.SingleAsync(f => f.Id == d.Id, TestContext.Current.CancellationToken);
        Folder eReloaded = await db.Folders.SingleAsync(f => f.Id == e.Id, TestContext.Current.CancellationToken);
        Folder otherReloaded = await db.Folders.SingleAsync(f => f.Id == other.Id, TestContext.Current.CancellationToken);

        cReloaded.Path.ShouldBe("/X/B/C");
        cReloaded.Depth.ShouldBe(3);
        dReloaded.Path.ShouldBe("/X/B/C/D");
        dReloaded.Depth.ShouldBe(4);
        eReloaded.Path.ShouldBe("/X/B/C/E");
        eReloaded.Depth.ShouldBe(4);

        // Unrelated sibling untouched.
        otherReloaded.Path.ShouldBe("/Other");
        otherReloaded.Depth.ShouldBe(1);
    }

    [Fact]
    public async Task MoveAsync_NullNewParent_PutsFolderUnderTenantRoot()
    {
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder leaf = await _sut.CreateAsync(a.Id, "Leaf", OwnerId, TestContext.Current.CancellationToken);

        Folder? moved = await _sut.MoveAsync(leaf.Id, newParentFolderId: null, TestContext.Current.CancellationToken);

        moved.ShouldNotBeNull();
        moved.Path.ShouldBe("/Leaf");
        moved.Depth.ShouldBe(1);
    }

    [Fact]
    public async Task MoveAsync_DescendantTarget_Throws()
    {
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await _sut.MoveAsync(a.Id, b.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveAsync_UnknownId_ReturnsNull()
    {
        Folder x = await _sut.CreateAsync(null, "X", OwnerId, TestContext.Current.CancellationToken);

        Folder? result = await _sut.MoveAsync(Guid.NewGuid(), x.Id, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task MoveAsync_PublishesTreePathChangedEvent_OnLocalBus()
    {
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, TestContext.Current.CancellationToken);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, TestContext.Current.CancellationToken);
        Folder x = await _sut.CreateAsync(null, "X", OwnerId, TestContext.Current.CancellationToken);

        _localEventBus.Captured.Clear();

        await _sut.MoveAsync(a.Id, x.Id, TestContext.Current.CancellationToken);

        FolderTreePathChangedEvent treeEvent = _localEventBus.Captured
            .OfType<FolderTreePathChangedEvent>()
            .ShouldHaveSingleItem();

        treeEvent.MovedFolderId.ShouldBe(a.Id);
        treeEvent.OldPathPrefix.ShouldBe("/A");
        treeEvent.NewPathPrefix.ShouldBe("/X/A");
        treeEvent.AffectedDescendantCount.ShouldBe(1); // B
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

    /// <summary>Captures every <c>PublishAsync</c> call so tests can assert event emission.</summary>
    private sealed class CapturingLocalEventBus : ILocalEventBus
    {
        public List<object> Captured { get; } = [];

        public Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            Captured.Add(localEvent);
            return Task.CompletedTask;
        }
    }
}

using Granit.Documents.Authorization;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// SQLite-backed unit tests for <see cref="EffectivePermissionResolver"/>. Verifies the
/// path-prefix resolution semantics, expiration handling, and "highest-permission wins"
/// rule without spinning up a Postgres container.
/// </summary>
public sealed class EffectivePermissionResolverTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<DocumentsDbContext> _options = null!;
    private TestFactory _factory = null!;
    private IClock _clock = null!;
    private EffectivePermissionResolver _sut = null!;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using DocumentsDbContext init = new(_options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestFactory(_options);
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(Now);
        _sut = new EffectivePermissionResolver(_factory, _clock);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task UnknownDocument_ReturnsNone()
    {
        var principal = DocumentPrincipal.ForUser(Guid.NewGuid());

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            Guid.NewGuid(), principal, TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task PrincipalWithoutGrantees_ReturnsNoneShortCircuit()
    {
        // Even when a global share would grant access, a principal with no UserId / roles /
        // groups must short-circuit to None — there is no identity to match.
        DocumentPrincipal principal = new(Guid.Empty, [], []);

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            Guid.NewGuid(), principal, TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task DirectDocumentShare_ReturnsExactPermission()
    {
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToDocument(
            Guid.NewGuid(), TenantId, doc.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Edit, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Edit);
        _ = folder;
    }

    [Fact]
    public async Task DirectFolderShare_AppliesToDocumentInThatFolder()
    {
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Read, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Read);
    }

    [Fact]
    public async Task AncestorFolderShare_InheritsToDeeplyNestedDocument()
    {
        // /Contracts (ancestor with Read) > /Contracts/2026 > document
        (Folder grandparent, _, Document doc) = await SeedDocumentInNestedFoldersAsync(
            "/Contracts", "/Contracts/2026");
        var user = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, grandparent.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Read, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Read);
    }

    [Fact]
    public async Task IsDefaultFolderShare_InheritsThroughThreeLevels()
    {
        // F6.4 acceptance: a folder share with IsDefault=true on /A applies to a document
        // sitting in /A/B/C — path-prefix inheritance must traverse the full ancestor chain.
        CancellationToken ct = TestContext.Current.CancellationToken;
        Folder root = await GetOrCreateRootAsync();
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(ct);
        Folder reloadedRoot = await db.Folders.SingleAsync(f => f.Id == root.Id, ct);

        var a = Folder.Create(Guid.NewGuid(), reloadedRoot, "A", OwnerId);
        db.Folders.Add(a);
        await db.SaveChangesAsync(ct);
        var b = Folder.Create(Guid.NewGuid(), a, "B", OwnerId);
        db.Folders.Add(b);
        await db.SaveChangesAsync(ct);
        var c = Folder.Create(Guid.NewGuid(), b, "C", OwnerId);
        var doc = Document.Create(Guid.NewGuid(), c, OwnerId, "deep.pdf");
        db.Folders.Add(c);
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        var user = Guid.NewGuid();
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, a.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Edit, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Edit);
    }

    [Fact]
    public async Task SiblingFolderShare_DoesNotLeakToAdjacentFolder()
    {
        // Grant on /A; document under /B/X — must NOT match.
        (Folder a, _) = await SeedDocumentInFolderAsync("/A");
        (Folder _, Document docInB) = await SeedDocumentInFolderAsync("/B");
        var user = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, a.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            docInB.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task PathPrefixCollision_DoesNotFalseMatch()
    {
        // /Contract and /Contracts share a leading prefix when matched naively as substrings,
        // but path-segment matching must NOT consider /Contract an ancestor of /Contracts/X.
        (Folder contract, _) = await SeedDocumentInFolderAsync("/Contract");
        (_, Document docInContracts) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, contract.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            docInContracts.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task HighestPermissionWins_AcrossOverlappingGrants()
    {
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();
        var role = Guid.NewGuid();

        // Read on the folder, Manage directly on the document (via role).
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Read, isDefault: true, OwnerId, Now));
        await SeedShareAsync(DocumentShare.ShareToDocument(
            Guid.NewGuid(), TenantId, doc.Id, ShareGranteeType.Role, role,
            SharePermissionLevel.Manage, OwnerId, Now));

        DocumentPrincipal principal = new(user, RoleIds: [role], GroupIds: []);

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, principal, TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Manage);
    }

    [Fact]
    public async Task GroupGrant_ResolvesViaGroupId()
    {
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var group = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.Group, group,
            SharePermissionLevel.Edit, isDefault: true, OwnerId, Now));

        DocumentPrincipal principal = new(Guid.NewGuid(), [], [group]);

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, principal, TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Edit);
    }

    [Fact]
    public async Task ExpiredGrant_IsIgnored()
    {
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();

        DateTimeOffset pastCreatedAt = Now.AddDays(-2);
        DateTimeOffset pastExpiresAt = Now.AddDays(-1);

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, isDefault: true, OwnerId, pastCreatedAt,
            expiresAt: pastExpiresAt));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task GetDocumentPermissionsAsync_ReturnsEntryForEveryRequestedId()
    {
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();
        var unknown = Guid.NewGuid();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Edit, isDefault: true, OwnerId, Now));

        IReadOnlyDictionary<Guid, EffectivePermissionLevel> result = await _sut
            .GetDocumentPermissionsAsync([doc.Id, unknown],
                DocumentPrincipal.ForUser(user),
                TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[doc.Id].ShouldBe(EffectivePermissionLevel.Edit);
        result[unknown].ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task GetDocumentPermissionsAsync_EmptyPrincipal_ReturnsNoneForAll()
    {
        (_, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        DocumentPrincipal empty = new(Guid.Empty, [], []);

        IReadOnlyDictionary<Guid, EffectivePermissionLevel> result = await _sut
            .GetDocumentPermissionsAsync([doc.Id],
                empty,
                TestContext.Current.CancellationToken);

        result[doc.Id].ShouldBe(EffectivePermissionLevel.None);
    }

    [Fact]
    public async Task GetDocumentPermissionsAsync_EmptyIds_ReturnsEmptyDictionary()
    {
        IReadOnlyDictionary<Guid, EffectivePermissionLevel> result = await _sut
            .GetDocumentPermissionsAsync([],
                DocumentPrincipal.ForUser(Guid.NewGuid()),
                TestContext.Current.CancellationToken);

        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task DifferentTenant_IsExcluded()
    {
        // Seed a doc in our TenantId, but a share row whose TenantId is different — even
        // though the FK targets exist, the resolver must filter by the document's tenant.
        (Folder folder, Document doc) = await SeedDocumentInFolderAsync("/Contracts");
        var user = Guid.NewGuid();

        var otherTenant = Guid.NewGuid();
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), otherTenant, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.None);
    }

    // ---- helpers --------------------------------------------------------

    private async Task<(Folder, Document)> SeedDocumentInFolderAsync(string folderPath)
    {
        Folder root = await GetOrCreateRootAsync();
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync();
        Folder reloadedRoot = await db.Folders.SingleAsync(f => f.Id == root.Id);
        // Strip leading "/" for Folder.Create's name parameter (it computes the path itself).
        var folder = Folder.Create(Guid.NewGuid(), reloadedRoot, folderPath[1..], OwnerId);
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "doc.pdf");
        db.Folders.Add(folder);
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return (folder, doc);
    }

    private async Task<(Folder grandparent, Folder parent, Document doc)>
        SeedDocumentInNestedFoldersAsync(string grandparentPath, string parentPath)
    {
        Folder root = await GetOrCreateRootAsync();
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync();
        Folder reloadedRoot = await db.Folders.SingleAsync(f => f.Id == root.Id);

        var grandparent = Folder.Create(Guid.NewGuid(), reloadedRoot, grandparentPath[1..], OwnerId);
        db.Folders.Add(grandparent);
        await db.SaveChangesAsync();

        // Compute the parent's tail name from the parentPath relative to the grandparent.
        string parentTail = parentPath[(grandparentPath.Length + 1)..];
        var parent = Folder.Create(Guid.NewGuid(), grandparent, parentTail, OwnerId);
        var doc = Document.Create(Guid.NewGuid(), parent, OwnerId, "doc.pdf");
        db.Folders.Add(parent);
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return (grandparent, parent, doc);
    }

    private async Task<Folder> GetOrCreateRootAsync()
    {
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync();
        Folder? root = await db.Folders.FirstOrDefaultAsync(f => f.IsTenantRoot);
        if (root is not null)
        {
            return root;
        }

        // Folder.CreateTenantRoot is internal — accessible via InternalsVisibleTo.
        var created = Folder.CreateTenantRoot(Guid.NewGuid(), TenantId, OwnerId);
        db.Folders.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    private async Task SeedShareAsync(DocumentShare share)
    {
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync();
        db.DocumentShares.Add(share);
        await db.SaveChangesAsync();
    }

    private sealed class TestFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }
}

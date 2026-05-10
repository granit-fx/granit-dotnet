using Granit.Documents;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed integration tests for <see cref="DocumentShareService"/> (F6.1).
/// Covers grant on folder + document + revoke + listing, plus the database-level CHECK
/// that pins the exactly-one-target invariant.
/// </summary>
public sealed class DocumentSharePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private DocumentShareService _sut = null!;
    private DocumentBootstrapService _bootstrap = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid GranteeId = Guid.NewGuid();

    public DocumentSharePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_shares, documents_document_versions, documents_documents, documents_folders RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(options);
        _bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero));

        _sut = new DocumentShareService(_factory, currentTenant, new SimpleGuidGenerator(), clock);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task GrantOnFolder_PersistsRowWithFolderTarget()
    {
        Folder folder = await SeedFolderAsync();

        DocumentShare? share = await _sut.GrantOnFolderAsync(
            folder.Id, ShareGranteeType.User, GranteeId, SharePermissionLevel.Read,
            isDefault: true, OwnerId, expiresAt: null,
            TestContext.Current.CancellationToken);

        share.ShouldNotBeNull();
        share.TargetType.ShouldBe(ShareTargetType.Folder);
        share.FolderId.ShouldBe(folder.Id);
        share.DocumentId.ShouldBeNull();
        share.IsDefault.ShouldBeTrue();

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        DocumentShare reloaded = await db.DocumentShares.SingleAsync(
            s => s.Id == share.Id, TestContext.Current.CancellationToken);
        reloaded.FolderId.ShouldBe(folder.Id);
        reloaded.DocumentId.ShouldBeNull();
        reloaded.GranteeType.ShouldBe(ShareGranteeType.User);
        reloaded.Permission.ShouldBe(SharePermissionLevel.Read);
    }

    [Fact]
    public async Task GrantOnFolder_MissingFolder_ReturnsNull()
    {
        DocumentShare? share = await _sut.GrantOnFolderAsync(
            Guid.NewGuid(), ShareGranteeType.User, GranteeId, SharePermissionLevel.Read,
            isDefault: true, OwnerId, expiresAt: null,
            TestContext.Current.CancellationToken);

        share.ShouldBeNull();
    }

    [Fact]
    public async Task GrantOnDocument_PersistsRowWithDocumentTarget()
    {
        (Folder folder, Document doc) = await SeedFolderAndDocumentAsync();

        DocumentShare? share = await _sut.GrantOnDocumentAsync(
            doc.Id, ShareGranteeType.Role, GranteeId, SharePermissionLevel.Edit,
            OwnerId, expiresAt: null,
            TestContext.Current.CancellationToken);

        share.ShouldNotBeNull();
        share.TargetType.ShouldBe(ShareTargetType.Document);
        share.DocumentId.ShouldBe(doc.Id);
        share.FolderId.ShouldBeNull();
        share.IsDefault.ShouldBeFalse();
        _ = folder; // used to seed the document
    }

    [Fact]
    public async Task RevokeAsync_DeletesRow()
    {
        Folder folder = await SeedFolderAsync();
        DocumentShare share = (await _sut.GrantOnFolderAsync(
            folder.Id, ShareGranteeType.Group, GranteeId, SharePermissionLevel.Manage,
            isDefault: false, OwnerId, expiresAt: null,
            TestContext.Current.CancellationToken))!;

        bool removed = await _sut.RevokeAsync(share.Id, TestContext.Current.CancellationToken);
        removed.ShouldBeTrue();

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.DocumentShares.AnyAsync(s => s.Id == share.Id, TestContext.Current.CancellationToken))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task RevokeAsync_MissingShare_ReturnsFalse() =>
        (await _sut.RevokeAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).ShouldBeFalse();

    [Fact]
    public async Task ListForFolderAsync_ExcludesExpiredAndOrdersByCreatedAt()
    {
        Folder folder = await SeedFolderAsync();
        DateTimeOffset now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

        // Manually craft a clock whose Now advances per call so CreatedAt diverges. The
        // queue holds one entry per expected access — three for the grant calls below plus
        // a "listing time" value used after the grants. NSubstitute's `Returns(...)` on a
        // property reads the property value first, which would dequeue an extra slot, so we
        // can't override the function-based stub mid-test; instead we let the queue carry
        // the listing-time value too.
        IClock clock = Substitute.For<IClock>();
        var queue = new Queue<DateTimeOffset>(
            [now, now.AddSeconds(1), now.AddSeconds(2), now.AddSeconds(10)]);
        clock.Now.Returns(_ => queue.Count > 1 ? queue.Dequeue() : queue.Peek());

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        var sut = new DocumentShareService(_factory, currentTenant, new SimpleGuidGenerator(), clock);

        DocumentShare a = (await sut.GrantOnFolderAsync(
            folder.Id, ShareGranteeType.User, GranteeId, SharePermissionLevel.Read,
            isDefault: true, OwnerId, expiresAt: null,
            TestContext.Current.CancellationToken))!;
        // expired's createdAt is dequeued as now+1s by the service — its expiresAt must be
        // strictly greater than that (DocumentShare invariant) yet still in the past relative
        // to the listing call below (now+10s).
        DocumentShare expired = (await sut.GrantOnFolderAsync(
            folder.Id, ShareGranteeType.User, Guid.NewGuid(), SharePermissionLevel.Read,
            isDefault: true, OwnerId, expiresAt: now.AddSeconds(5),
            TestContext.Current.CancellationToken))!;
        DocumentShare c = (await sut.GrantOnFolderAsync(
            folder.Id, ShareGranteeType.Role, Guid.NewGuid(), SharePermissionLevel.Edit,
            isDefault: true, OwnerId, expiresAt: null,
            TestContext.Current.CancellationToken))!;

        // List uses the service's clock; the queue's last entry (now+10s, peeked from now
        // on) puts us safely past `expired.ExpiresAt`.
        IReadOnlyList<DocumentShare> rows = await sut
            .ListForFolderAsync(folder.Id, TestContext.Current.CancellationToken);

        rows.Select(s => s.Id).ShouldBe([a.Id, c.Id]);
        rows.Any(s => s.Id == expired.Id).ShouldBeFalse();
    }

    [Fact]
    public async Task CheckConstraint_InconsistentRow_RaisesDbException()
    {
        // Bypass the aggregate factory to attempt persisting a row that violates
        // ck_documents_shares_target_exactly_one. This proves the database guard,
        // not just the C# invariant.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_shares;",
            TestContext.Current.CancellationToken);

        // Pass parameters via the IEnumerable<object> overload so the CancellationToken
        // doesn't get folded into the params array (which would trigger an
        // InvalidOperationException about parameter binding before the constraint can fire).
        object[] parameters =
        [
            new Npgsql.NpgsqlParameter("id", Guid.NewGuid()),
            new Npgsql.NpgsqlParameter("tenant", TenantId),
            new Npgsql.NpgsqlParameter("grantee", GranteeId),
            new Npgsql.NpgsqlParameter("creator", OwnerId),
        ];
        // ExecuteSqlRawAsync propagates the provider exception directly (no DbUpdateException
        // wrap — that is reserved for SaveChanges). A failed CHECK constraint surfaces as a
        // Npgsql PostgresException with SqlState 23514.
        await Should.ThrowAsync<Npgsql.PostgresException>(async () =>
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO documents_shares
                    ("Id", "TenantId", "TargetType", "FolderId", "DocumentId",
                     "GranteeType", "GranteeId", "Permission", "IsDefault",
                     "ExpiresAt", "CreatedAt", "CreatedByUserId")
                VALUES
                    (@id, @tenant, 'Folder', NULL, NULL,
                     'User', @grantee, 'Read', TRUE,
                     NULL, NOW(), @creator);
                """,
                parameters,
                TestContext.Current.CancellationToken);
        });
    }

    private async Task<Folder> SeedFolderAsync()
    {
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId, TestContext.Current.CancellationToken);
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder root = await db.Folders.SingleAsync(f => f.Id == rootId, TestContext.Current.CancellationToken);
        var folder = Folder.Create(Guid.NewGuid(), root, "Contracts", OwnerId);
        db.Folders.Add(folder);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return folder;
    }

    private async Task<(Folder, Document)> SeedFolderAndDocumentAsync()
    {
        Folder folder = await SeedFolderAsync();
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder reloaded = await db.Folders.SingleAsync(f => f.Id == folder.Id, TestContext.Current.CancellationToken);
        var doc = Document.Create(Guid.NewGuid(), reloaded, OwnerId, "Contract.pdf");
        db.Documents.Add(doc);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (folder, doc);
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

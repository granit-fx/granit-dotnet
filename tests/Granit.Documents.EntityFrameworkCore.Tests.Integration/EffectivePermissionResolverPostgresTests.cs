using Granit.Documents.Authorization;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Guids;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed test that verifies the F6.2 share-resolution query uses the filtered
/// indexes (<c>ix_documents_shares_grantee_folder</c> / <c>ix_documents_shares_grantee_document</c>)
/// introduced in F6.1. Without index usage the resolver collapses to a sequential scan
/// under load — that regression is what this test guards against.
/// </summary>
public sealed class EffectivePermissionResolverPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private EffectivePermissionResolver _sut = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public EffectivePermissionResolverPostgresTests(PostgresFixture postgres) => _postgres = postgres;

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

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        _sut = new EffectivePermissionResolver(_factory, clock);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task FolderShare_ResolvesToHighestPermission_OnPostgres()
    {
        (Folder folder, Document doc, Guid user) = await SeedAsync();

        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Edit, isDefault: true, OwnerId, Now));
        await SeedShareAsync(DocumentShare.ShareToDocument(
            Guid.NewGuid(), TenantId, doc.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Manage);
    }

    [Fact]
    public async Task IsDefaultFolderShare_InheritsAcrossThreeLevels_OnPostgres()
    {
        // F6.4 acceptance — same-tree, three-level deep inheritance through /A/B/C with
        // explicit IsDefault=true. Pinned on Postgres to catch any provider-specific quirk
        // in the path-prefix predicate (the SQLite suite covers the unit-level case).
        var bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        Guid rootId = await bootstrap.EnsureTenantRootAsync(TenantId, OwnerId, TestContext.Current.CancellationToken);

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder root = await db.Folders.SingleAsync(f => f.Id == rootId, TestContext.Current.CancellationToken);
        var a = Folder.Create(Guid.NewGuid(), root, "A", OwnerId);
        db.Folders.Add(a);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var b = Folder.Create(Guid.NewGuid(), a, "B", OwnerId);
        db.Folders.Add(b);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var c = Folder.Create(Guid.NewGuid(), b, "C", OwnerId);
        var doc = Document.Create(Guid.NewGuid(), c, OwnerId, "deep.pdf");
        db.Folders.Add(c);
        db.Documents.Add(doc);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var user = Guid.NewGuid();
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, a.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Edit, isDefault: true, OwnerId, Now));

        EffectivePermissionLevel result = await _sut.GetDocumentPermissionAsync(
            doc.Id, DocumentPrincipal.ForUser(user), TestContext.Current.CancellationToken);

        result.ShouldBe(EffectivePermissionLevel.Edit);
    }

    [Fact]
    public async Task ShareQuery_PrefersIndexOverSeqScan_OnPostgres()
    {
        // Seed enough rows to push the planner away from a seq scan. Postgres requires
        // ANALYZE before the row estimate guides the planner.
        (Folder folder, Document doc, Guid user) = await SeedAsync();
        for (int i = 0; i < 200; i++)
        {
            await SeedShareAsync(DocumentShare.ShareToFolder(
                Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, Guid.NewGuid(),
                SharePermissionLevel.Read, isDefault: true, OwnerId, Now));
        }
        await SeedShareAsync(DocumentShare.ShareToFolder(
            Guid.NewGuid(), TenantId, folder.Id, ShareGranteeType.User, user,
            SharePermissionLevel.Manage, isDefault: true, OwnerId, Now));

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "ANALYZE documents_shares;",
            TestContext.Current.CancellationToken);

        // EXPLAIN the same shape the resolver issues. We bind the predicates that match the
        // filtered index ix_documents_shares_grantee_folder.
        const string explainSql = """
            EXPLAIN (FORMAT TEXT)
            SELECT s."Permission"
            FROM documents_shares s
            WHERE s."TenantId" = @tenant
              AND s."GranteeId" = ANY(@grantees)
              AND s."TargetType" = 'Folder'
              AND s."FolderId" = @folder;
            """;

        // Query the plan via the underlying connection so we can capture the textual rows.
        await using Npgsql.NpgsqlConnection conn = new(_postgres.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using Npgsql.NpgsqlCommand cmd = new(explainSql, conn);
        cmd.Parameters.AddWithValue("tenant", TenantId);
        cmd.Parameters.AddWithValue("grantees", new[] { user });
        cmd.Parameters.AddWithValue("folder", folder.Id);

        List<string> planLines = [];
        await using (Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                planLines.Add(reader.GetString(0));
            }
        }
        string plan = string.Join('\n', planLines);

        // The filtered index ix_documents_shares_grantee_folder must show up in the plan.
        plan.ShouldContain(
            "ix_documents_shares_grantee_folder",
            customMessage: $"Expected filtered index to be used. Plan was:\n{plan}");
        _ = doc; // doc is reused only to anchor the seeded data graph.
    }

    private async Task<(Folder, Document, Guid user)> SeedAsync()
    {
        var user = Guid.NewGuid();
        // Use the public bootstrap service to create the tenant root — Folder.CreateTenantRoot
        // is internal and the integration-test project is not in the InternalsVisibleTo list.
        var bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());
        Guid rootId = await bootstrap.EnsureTenantRootAsync(TenantId, OwnerId, TestContext.Current.CancellationToken);

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder root = await db.Folders.SingleAsync(f => f.Id == rootId, TestContext.Current.CancellationToken);
        var folder = Folder.Create(Guid.NewGuid(), root, "Contracts", OwnerId);
        var doc = Document.Create(Guid.NewGuid(), folder, OwnerId, "doc.pdf");
        db.Folders.Add(folder);
        db.Documents.Add(doc);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (folder, doc, user);
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private async Task SeedShareAsync(DocumentShare share)
    {
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.DocumentShares.Add(share);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }
}

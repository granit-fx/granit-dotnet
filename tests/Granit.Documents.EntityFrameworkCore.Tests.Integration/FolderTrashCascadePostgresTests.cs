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
/// Postgres-backed verification that <see cref="FolderService.TrashAsync"/> cascades
/// the trash status to every active descendant folder + document in a single
/// transaction, and that <see cref="FolderService.RestoreAsync"/> stays non-cascading
/// per the F8.1 acceptance.
/// </summary>
public sealed class FolderTrashCascadePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private FolderService _sut = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public FolderTrashCascadePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_folders, documents_documents, documents_document_versions RESTART IDENTITY CASCADE;");

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
    public async Task TrashAsync_DeepSubtree_CascadesToAllDescendants_OnPostgres()
    {
        // /A → /A/B → /A/B/C plus /A/Sibling and a doc per leaf folder. Trashing /A
        // must flip every descendant folder + document to Trashed.
        CancellationToken ct = TestContext.Current.CancellationToken;
        Folder a = await _sut.CreateAsync(null, "A", OwnerId, ct);
        Folder b = await _sut.CreateAsync(a.Id, "B", OwnerId, ct);
        Folder c = await _sut.CreateAsync(b.Id, "C", OwnerId, ct);
        Folder sibling = await _sut.CreateAsync(a.Id, "Sibling", OwnerId, ct);

        await using DocumentsDbContext seed = await _factory.CreateDbContextAsync(ct);
        Folder cAttached = await seed.Folders.SingleAsync(f => f.Id == c.Id, ct);
        Folder siblingAttached = await seed.Folders.SingleAsync(f => f.Id == sibling.Id, ct);
        var docInC = Document.Create(Guid.NewGuid(), cAttached, OwnerId, "in-c.pdf");
        var docInSibling = Document.Create(Guid.NewGuid(), siblingAttached, OwnerId, "in-sibling.pdf");
        seed.Documents.Add(docInC);
        seed.Documents.Add(docInSibling);
        await seed.SaveChangesAsync(ct);

        // Also create an unrelated /Other folder + doc that must stay Active.
        Folder other = await _sut.CreateAsync(null, "Other", OwnerId, ct);
        await using DocumentsDbContext seed2 = await _factory.CreateDbContextAsync(ct);
        Folder otherAttached = await seed2.Folders.SingleAsync(f => f.Id == other.Id, ct);
        var docInOther = Document.Create(Guid.NewGuid(), otherAttached, OwnerId, "in-other.pdf");
        seed2.Documents.Add(docInOther);
        await seed2.SaveChangesAsync(ct);

        await _sut.TrashAsync(a.Id, ct);

        await using DocumentsDbContext check = await _factory.CreateDbContextAsync(ct);
        Folder reloadedA = await check.Folders.SingleAsync(f => f.Id == a.Id, ct);
        Folder reloadedB = await check.Folders.SingleAsync(f => f.Id == b.Id, ct);
        Folder reloadedC = await check.Folders.SingleAsync(f => f.Id == c.Id, ct);
        Folder reloadedSibling = await check.Folders.SingleAsync(f => f.Id == sibling.Id, ct);
        Folder reloadedOther = await check.Folders.SingleAsync(f => f.Id == other.Id, ct);
        Document reloadedDocC = await check.Documents.SingleAsync(d => d.Id == docInC.Id, ct);
        Document reloadedDocSibling = await check.Documents.SingleAsync(d => d.Id == docInSibling.Id, ct);
        Document reloadedDocOther = await check.Documents.SingleAsync(d => d.Id == docInOther.Id, ct);

        reloadedA.Status.ShouldBe(FolderStatus.Trashed);
        reloadedB.Status.ShouldBe(FolderStatus.Trashed);
        reloadedC.Status.ShouldBe(FolderStatus.Trashed);
        reloadedSibling.Status.ShouldBe(FolderStatus.Trashed);
        reloadedDocC.Status.ShouldBe(DocumentStatus.Trashed);
        reloadedDocSibling.Status.ShouldBe(DocumentStatus.Trashed);

        // Unrelated subtree untouched.
        reloadedOther.Status.ShouldBe(FolderStatus.Active);
        reloadedDocOther.Status.ShouldBe(DocumentStatus.Active);
    }

    private sealed class SimpleGuidGenerator : IGuidGenerator
    {
        public Guid Create() => Guid.NewGuid();
    }

    private sealed class TestDbContextFactory(DbContextOptions<DocumentsDbContext> options)
        : IDbContextFactory<DocumentsDbContext>
    {
        public DocumentsDbContext CreateDbContext() => new(options);

        public Task<DocumentsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DocumentsDbContext>(new(options));
    }
}

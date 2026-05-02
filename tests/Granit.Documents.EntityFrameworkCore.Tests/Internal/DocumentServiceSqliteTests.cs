using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.Documents;
using Granit.Documents.Diagnostics;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Events;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Internal;

/// <summary>
/// SQLite-backed integration tests for <see cref="DocumentService"/>. Exercises the
/// upload-finalise flow by mocking <see cref="IBlobStorage"/> and asserting the
/// persisted Document + initial v1 DocumentVersion + emitted events.
/// </summary>
public sealed class DocumentServiceSqliteTests : IAsyncLifetime
{
    private SqliteConnection _holdOpen = null!;
    private TestDbContextFactory _factory = null!;
    private DocumentBootstrapService _bootstrap = null!;
    private IBlobStorage _blobStorage = null!;
    private CapturingLocalEventBus _localEventBus = null!;
    private DocumentService _sut = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            $"DataSource=file:f32-doc-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _holdOpen = new SqliteConnection(connectionString);
        await _holdOpen.OpenAsync();

        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();

        _factory = new TestDbContextFactory(options);
        _bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        _blobStorage = Substitute.For<IBlobStorage>();
        _localEventBus = new CapturingLocalEventBus();

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        _sut = new DocumentService(
            _factory,
            _bootstrap,
            _blobStorage,
            currentTenant,
            new SimpleGuidGenerator(),
            clock,
            _localEventBus,
            metrics);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    [Fact]
    public async Task RequestUploadTicketAsync_DelegatesToBlobStorage()
    {
        var ticket = new PresignedUploadTicket(
            BlobId: Guid.NewGuid(),
            UploadUrl: new Uri("https://storage.test/blob/12345"),
            HttpMethod: "PUT",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15),
            RequiredHeaders: new Dictionary<string, string> { ["Content-Type"] = "application/pdf" });
        _blobStorage.InitiateUploadAsync(
            DocumentService.ContainerName, Arg.Any<BlobUploadRequest>(), Arg.Any<CancellationToken>())
            .Returns(ticket);

        PresignedUploadTicket result = await _sut.RequestUploadTicketAsync(
            "contract.pdf", "application/pdf", 1_000_000, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(ticket);
    }

    [Fact]
    public async Task FinalizeUploadAsync_HappyPath_CreatesDocumentAndInitialVersion()
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true,
                Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf",
                SizeBytes: 1_234_567,
                RejectionReason: null));

        Document doc = await _sut.FinalizeUploadAsync(
            blobId, folderId: null, OwnerId, "Q1-2026.pdf", "Quarter 1 close", "Initial",
            TestContext.Current.CancellationToken);

        doc.TenantId.ShouldBe(TenantId);
        doc.Name.ShouldBe("Q1-2026.pdf");
        doc.OwnerUserId.ShouldBe(OwnerId);
        doc.Status.ShouldBe(DocumentStatus.Active);
        doc.CurrentVersionId.ShouldNotBeNull();

        // Verify the version row was inserted with VersionNumber = 1 and the verified
        // BlobStorage metadata.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        DocumentVersion version = await db.DocumentVersions
            .SingleAsync(v => v.DocumentId == doc.Id, TestContext.Current.CancellationToken);
        version.VersionNumber.ShouldBe(1);
        version.BlobDescriptorId.ShouldBe(blobId);
        version.SizeBytes.ShouldBe(1_234_567);
        version.ContentType.ShouldBe("application/pdf");
        version.CommitMessage.ShouldBe("Initial");
        version.Id.ShouldBe(doc.CurrentVersionId!.Value);

        // DocumentVersionAddedEvent emitted on the local bus.
        DocumentVersionAddedEvent ev = _localEventBus.Captured
            .OfType<DocumentVersionAddedEvent>().ShouldHaveSingleItem();
        ev.DocumentId.ShouldBe(doc.Id);
        ev.VersionNumber.ShouldBe(1);
        ev.BlobDescriptorId.ShouldBe(blobId);
    }

    [Fact]
    public async Task FinalizeUploadAsync_BlobInvalid_Throws()
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: false,
                Status: BlobStatus.Rejected,
                VerifiedContentType: null,
                SizeBytes: null,
                RejectionReason: "Magic-bytes mismatch"));

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.FinalizeUploadAsync(
                blobId, null, OwnerId, "F.pdf", null, null, TestContext.Current.CancellationToken));

        // No document row was created.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Documents.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task FinalizeUploadAsync_MissingFolder_Throws()
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf", SizeBytes: 100, RejectionReason: null));

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.FinalizeUploadAsync(
                blobId, folderId: Guid.NewGuid(), OwnerId, "F.pdf", null, null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FinalizeUploadAsync_TargetSpecificFolder_CreatesUnderIt()
    {
        // Create a folder via the bootstrap + folder service path.
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId,
            TestContext.Current.CancellationToken);

        await using (DocumentsDbContext seed = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            Folder root = await seed.Folders.SingleAsync(f => f.Id == rootId,
                TestContext.Current.CancellationToken);
            seed.Folders.Add(Folder.Create(Guid.NewGuid(), root, "Contracts", OwnerId));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Folder contracts;
        await using (DocumentsDbContext q = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            contracts = await q.Folders.SingleAsync(f => f.Name == "Contracts",
                TestContext.Current.CancellationToken);
        }

        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "text/plain", SizeBytes: 42, RejectionReason: null));

        Document doc = await _sut.FinalizeUploadAsync(
            blobId, contracts.Id, OwnerId, "Note.txt", null, null,
            TestContext.Current.CancellationToken);

        doc.FolderId.ShouldBe(contracts.Id);
    }

    // -------------------------------------------------------------------------
    // F3.3 — RequestDownloadUrlAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RequestDownloadUrlAsync_HappyPath_ReturnsUrlAndEmitsAuditEvent()
    {
        // Seed a document via the upload flow.
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf", SizeBytes: 100, RejectionReason: null));
        Document doc = await _sut.FinalizeUploadAsync(
            blobId, null, OwnerId, "F.pdf", null, null, TestContext.Current.CancellationToken);

        // Stub the download URL.
        DateTimeOffset expires = DateTimeOffset.UtcNow.AddMinutes(15);
        var presigned = new PresignedDownloadUrl(new Uri("https://storage.test/dl/abc"), expires);
        _blobStorage
            .CreateDownloadUrlAsync(DocumentService.ContainerName, blobId, options: null, Arg.Any<CancellationToken>())
            .Returns(presigned);

        _localEventBus.Captured.Clear();
        var requester = Guid.NewGuid();

        PresignedDownloadUrl? url = await _sut.RequestDownloadUrlAsync(
            doc.Id, versionId: null, requester, TestContext.Current.CancellationToken);

        url.ShouldNotBeNull();
        url.Url.ShouldBe(presigned.Url);
        url.ExpiresAt.ShouldBe(expires);

        DocumentDownloadedEvent ev = _localEventBus.Captured
            .OfType<DocumentDownloadedEvent>().ShouldHaveSingleItem();
        ev.DocumentId.ShouldBe(doc.Id);
        ev.RequestedByUserId.ShouldBe(requester);
        ev.UrlExpiresAt.ShouldBe(expires);
        ev.VersionId.ShouldBe(doc.CurrentVersionId!.Value);
    }

    [Fact]
    public async Task RequestDownloadUrlAsync_UnknownDocument_ReturnsNull()
    {
        PresignedDownloadUrl? url = await _sut.RequestDownloadUrlAsync(
            Guid.NewGuid(), versionId: null, requestedByUserId: Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        url.ShouldBeNull();
    }

    [Fact]
    public async Task RequestDownloadUrlAsync_TrashedDocument_Throws()
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf", SizeBytes: 100, RejectionReason: null));
        Document doc = await _sut.FinalizeUploadAsync(
            blobId, null, OwnerId, "F.pdf", null, null, TestContext.Current.CancellationToken);

        // Trash the document directly via the aggregate + DbContext.
        await using (DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            Document tracked = await db.Documents.SingleAsync(d => d.Id == doc.Id,
                TestContext.Current.CancellationToken);
            tracked.Trash(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.RequestDownloadUrlAsync(doc.Id, null, Guid.NewGuid(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestDownloadUrlAsync_SpecificVersion_ResolvesIt()
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "text/plain", SizeBytes: 1, RejectionReason: null));
        Document doc = await _sut.FinalizeUploadAsync(
            blobId, null, OwnerId, "F.txt", null, null, TestContext.Current.CancellationToken);

        Guid versionId = doc.CurrentVersionId!.Value;
        _blobStorage
            .CreateDownloadUrlAsync(DocumentService.ContainerName, blobId, options: null, Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(new Uri("https://storage.test/dl/v1"), DateTimeOffset.UtcNow.AddMinutes(5)));

        PresignedDownloadUrl? url = await _sut.RequestDownloadUrlAsync(
            doc.Id, versionId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        url.ShouldNotBeNull();
    }

    [Fact]
    public async Task RequestDownloadUrlAsync_SpecificVersion_NotFound_ReturnsNull()
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "text/plain", SizeBytes: 1, RejectionReason: null));
        Document doc = await _sut.FinalizeUploadAsync(
            blobId, null, OwnerId, "F.txt", null, null, TestContext.Current.CancellationToken);

        PresignedDownloadUrl? url = await _sut.RequestDownloadUrlAsync(
            doc.Id, versionId: Guid.NewGuid(), Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        url.ShouldBeNull();
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

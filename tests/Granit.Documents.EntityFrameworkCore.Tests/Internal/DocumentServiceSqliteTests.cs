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
        // Default descriptor stub so the F7.2 pre-confirm quota lookup doesn't error out
        // for tests that only stub ConfirmUploadAsync. Tests overriding the descriptor —
        // e.g. to test quota over-reservation release — substitute their own.
        _blobStorage.GetDescriptorAsync(
                DocumentService.ContainerName, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => StubDescriptor((Guid)ci[1], maxAllowedBytes: long.MaxValue));
        _localEventBus = new CapturingLocalEventBus();

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();
        quotas.TryReserveAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _sut = new DocumentService(
            _factory,
            _bootstrap,
            _blobStorage,
            currentTenant,
            new SimpleGuidGenerator(),
            clock,
            _localEventBus,
            metrics,
            quotas);
    }

    public ValueTask DisposeAsync() => _holdOpen.DisposeAsync();

    private static BlobDescriptor StubDescriptor(Guid blobId, long maxAllowedBytes) =>
        BlobDescriptor.Create(
            blobId,
            tenantId: TenantId,
            containerName: DocumentService.ContainerName,
            objectKey: $"docs/{blobId:N}",
            request: new BlobUploadRequest("file.pdf", "application/pdf", maxAllowedBytes),
            createdAt: DateTimeOffset.UtcNow);

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
    public async Task FinalizeUploadAsync_QuotaExceeded_Throws_AndDoesNotConfirmBlob()
    {
        // F7.2: when the tenant quota is exhausted, finalize must throw the quota-exceeded
        // exception, leave the blob in Pending status (no ConfirmUploadAsync call), and
        // record the quota.rejected counter.
        var blobId = Guid.NewGuid();
        _blobStorage.GetDescriptorAsync(
                DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(StubDescriptor(blobId, maxAllowedBytes: 1_000_000));

        // Override the quota stub built in InitializeAsync — caller-specific instance.
        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();
        quotas.TryReserveAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(false); // quota would be exceeded

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        var sut = new DocumentService(
            _factory, _bootstrap, _blobStorage, tenant,
            new SimpleGuidGenerator(), clock, _localEventBus, metrics, quotas);

        Granit.Documents.Exceptions.TenantStorageQuotaExceededException ex =
            await Should.ThrowAsync<Granit.Documents.Exceptions.TenantStorageQuotaExceededException>(async () =>
                await sut.FinalizeUploadAsync(
                    blobId, null, OwnerId, "F.pdf", null, null, TestContext.Current.CancellationToken));
        ex.TenantId.ShouldBe(TenantId);
        ex.RequestedBytes.ShouldBe(1_000_000);

        await _blobStorage.DidNotReceive()
            .ConfirmUploadAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // No document row created.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        (await db.Documents.AnyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task FinalizeUploadAsync_OverReservation_IsReleasedAfterConfirm()
    {
        // F7.2 release path: declared MaxAllowedBytes (1 MB) but real SizeBytes (100 KB)
        // means the service must Decrement the slack so UsageBytes ends up at the actual
        // size, not the declared upper bound.
        var blobId = Guid.NewGuid();
        _blobStorage.GetDescriptorAsync(
                DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(StubDescriptor(blobId, maxAllowedBytes: 1_000_000));
        _blobStorage.ConfirmUploadAsync(
                DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf",
                SizeBytes: 100_000, RejectionReason: null));

        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();
        quotas.TryReserveAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(TenantId);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        var sut = new DocumentService(
            _factory, _bootstrap, _blobStorage, tenant,
            new SimpleGuidGenerator(), clock, _localEventBus, metrics, quotas);

        await sut.FinalizeUploadAsync(
            blobId, null, OwnerId, "F.pdf", null, null, TestContext.Current.CancellationToken);

        await quotas.Received(1).TryReserveAsync(TenantId, 1_000_000, Arg.Any<CancellationToken>());
        await quotas.Received(1).DecrementAsync(TenantId, 900_000, Arg.Any<CancellationToken>());
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

    // -------------------------------------------------------------------------
    // F3.4 — Rename / UpdateDescription / Move / Trash / GetByIdAsync
    // -------------------------------------------------------------------------

    private async Task<Document> SeedDocumentAsync(string name = "F.pdf", Guid? folderId = null)
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf", SizeBytes: 100, RejectionReason: null));
        return await _sut.FinalizeUploadAsync(blobId, folderId, OwnerId, name, null, null,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetByIdAsync_Existing_ReturnsDocument()
    {
        Document seeded = await SeedDocumentAsync();

        Document? fetched = await _sut.GetByIdAsync(seeded.Id, TestContext.Current.CancellationToken);

        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(seeded.Id);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_ReturnsNull()
    {
        Document? fetched = await _sut.GetByIdAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        fetched.ShouldBeNull();
    }

    [Fact]
    public async Task RenameAsync_UpdatesNameAndIncrementsRowVersion()
    {
        Document seeded = await SeedDocumentAsync("Old.pdf");
        uint before = seeded.RowVersion;

        Document? renamed = await _sut.RenameAsync(seeded.Id, "New.pdf",
            TestContext.Current.CancellationToken);

        renamed.ShouldNotBeNull();
        renamed.Name.ShouldBe("New.pdf");
        renamed.RowVersion.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task RenameAsync_Missing_ReturnsNull()
    {
        Document? result = await _sut.RenameAsync(Guid.NewGuid(), "X.pdf",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateDescriptionAsync_UpdatesValue()
    {
        Document seeded = await SeedDocumentAsync();

        Document? result = await _sut.UpdateDescriptionAsync(seeded.Id, "fresh",
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Description.ShouldBe("fresh");
    }

    [Fact]
    public async Task UpdateDescriptionAsync_NullClears()
    {
        Document seeded = await SeedDocumentAsync();
        await _sut.UpdateDescriptionAsync(seeded.Id, "non-null",
            TestContext.Current.CancellationToken);

        Document? result = await _sut.UpdateDescriptionAsync(seeded.Id, null,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task MoveAsync_DifferentFolder_UpdatesFolderId()
    {
        Document seeded = await SeedDocumentAsync();
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId,
            TestContext.Current.CancellationToken);

        await using DocumentsDbContext seed = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Folder rootFolder = await seed.Folders.SingleAsync(f => f.Id == rootId,
            TestContext.Current.CancellationToken);
        var contracts = Folder.Create(Guid.NewGuid(), rootFolder, "Contracts", OwnerId);
        seed.Folders.Add(contracts);
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);

        Document? moved = await _sut.MoveAsync(seeded.Id, contracts.Id,
            TestContext.Current.CancellationToken);

        moved.ShouldNotBeNull();
        moved.FolderId.ShouldBe(contracts.Id);
    }

    [Fact]
    public async Task MoveAsync_NullNewFolder_PutsDocumentUnderTenantRoot()
    {
        // Seed a document under a sub-folder, then move it to null (tenant root).
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId,
            TestContext.Current.CancellationToken);
        Folder contracts;
        await using (DocumentsDbContext seed = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            Folder rootFolder = await seed.Folders.SingleAsync(f => f.Id == rootId,
                TestContext.Current.CancellationToken);
            contracts = Folder.Create(Guid.NewGuid(), rootFolder, "Contracts", OwnerId);
            seed.Folders.Add(contracts);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Document seeded = await SeedDocumentAsync(folderId: contracts.Id);

        Document? moved = await _sut.MoveAsync(seeded.Id, newFolderId: null,
            TestContext.Current.CancellationToken);

        moved.ShouldNotBeNull();
        moved.FolderId.ShouldBe(rootId);
    }

    [Fact]
    public async Task MoveAsync_TrashedFolder_Throws()
    {
        Document seeded = await SeedDocumentAsync();
        Guid rootId = await _bootstrap.EnsureTenantRootAsync(TenantId, OwnerId,
            TestContext.Current.CancellationToken);

        Folder trashedFolder;
        await using (DocumentsDbContext seed = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            Folder rootFolder = await seed.Folders.SingleAsync(f => f.Id == rootId,
                TestContext.Current.CancellationToken);
            trashedFolder = Folder.Create(Guid.NewGuid(), rootFolder, "Garbage", OwnerId);
            trashedFolder.Trash(DateTimeOffset.UtcNow);
            seed.Folders.Add(trashedFolder);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.MoveAsync(seeded.Id, trashedFolder.Id,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveAsync_MissingTargetFolder_Throws()
    {
        Document seeded = await SeedDocumentAsync();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.MoveAsync(seeded.Id, Guid.NewGuid(),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveAsync_Missing_ReturnsNull()
    {
        Document? result = await _sut.MoveAsync(Guid.NewGuid(), null,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TrashAsync_SetsStatusAndTrashedAt()
    {
        Document seeded = await SeedDocumentAsync();

        Document? trashed = await _sut.TrashAsync(seeded.Id,
            TestContext.Current.CancellationToken);

        trashed.ShouldNotBeNull();
        trashed.Status.ShouldBe(DocumentStatus.Trashed);
        trashed.TrashedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task TrashAsync_Missing_ReturnsNull()
    {
        Document? result = await _sut.TrashAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_TrashedDocument_ReturnsActive()
    {
        Document seeded = await SeedDocumentAsync();
        await _sut.TrashAsync(seeded.Id, TestContext.Current.CancellationToken);

        Document? restored = await _sut.RestoreAsync(seeded.Id,
            TestContext.Current.CancellationToken);

        restored.ShouldNotBeNull();
        restored.Status.ShouldBe(DocumentStatus.Active);
        restored.TrashedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_NotTrashed_ReturnsNull()
    {
        Document seeded = await SeedDocumentAsync();

        Document? result = await _sut.RestoreAsync(seeded.Id,
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_Missing_ReturnsNull()
    {
        Document? result = await _sut.RestoreAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // F4.1 — AppendVersionAsync (autonomous monotonic versioning)
    // -------------------------------------------------------------------------

    private void StubBlobConfirmation(Guid blobId, string contentType = "application/pdf", long sizeBytes = 100)
    {
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: contentType, SizeBytes: sizeBytes, RejectionReason: null));
    }

    [Fact]
    public async Task AppendVersionAsync_HappyPath_CreatesV2_UpdatesCurrentVersionPointer_EmitsEvent()
    {
        Document seeded = await SeedDocumentAsync();
        Guid v1Id = seeded.CurrentVersionId!.Value;
        uint rvBefore = seeded.RowVersion;
        _localEventBus.Captured.Clear();

        var v2BlobId = Guid.NewGuid();
        StubBlobConfirmation(v2BlobId, sizeBytes: 200);

        DocumentVersion? v2 = await _sut.AppendVersionAsync(
            seeded.Id, v2BlobId, OwnerId, "v2 commit", TestContext.Current.CancellationToken);

        v2.ShouldNotBeNull();
        v2.VersionNumber.ShouldBe(2);
        v2.DocumentId.ShouldBe(seeded.Id);
        v2.BlobDescriptorId.ShouldBe(v2BlobId);
        v2.SizeBytes.ShouldBe(200);
        v2.CommitMessage.ShouldBe("v2 commit");
        v2.Id.ShouldNotBe(v1Id);

        // Document.CurrentVersionId moved to the new version + RowVersion bumped.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Document persisted = await db.Documents.SingleAsync(d => d.Id == seeded.Id,
            TestContext.Current.CancellationToken);
        persisted.CurrentVersionId.ShouldBe(v2.Id);
        persisted.RowVersion.ShouldBeGreaterThan(rvBefore);

        // Version history holds both rows.
        List<DocumentVersion> versions = await db.DocumentVersions
            .Where(v => v.DocumentId == seeded.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(TestContext.Current.CancellationToken);
        versions.Count.ShouldBe(2);
        versions[0].VersionNumber.ShouldBe(1);
        versions[1].VersionNumber.ShouldBe(2);

        // Event emission.
        DocumentVersionAddedEvent ev = _localEventBus.Captured
            .OfType<DocumentVersionAddedEvent>().ShouldHaveSingleItem();
        ev.VersionNumber.ShouldBe(2);
        ev.VersionId.ShouldBe(v2.Id);
        ev.BlobDescriptorId.ShouldBe(v2BlobId);
    }

    [Fact]
    public async Task AppendVersionAsync_SequentialAppends_VersionNumbersAreMonotonic()
    {
        Document seeded = await SeedDocumentAsync();

        for (int expected = 2; expected <= 5; expected++)
        {
            var blobId = Guid.NewGuid();
            StubBlobConfirmation(blobId, sizeBytes: expected * 100L);

            DocumentVersion? created = await _sut.AppendVersionAsync(
                seeded.Id, blobId, OwnerId, $"v{expected}", TestContext.Current.CancellationToken);

            created.ShouldNotBeNull();
            created.VersionNumber.ShouldBe(expected);
        }

        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        List<int> numbers = await db.DocumentVersions
            .Where(v => v.DocumentId == seeded.Id)
            .OrderBy(v => v.VersionNumber)
            .Select(v => v.VersionNumber)
            .ToListAsync(TestContext.Current.CancellationToken);
        numbers.ShouldBe([1, 2, 3, 4, 5]);

        Document final = await db.Documents.SingleAsync(d => d.Id == seeded.Id,
            TestContext.Current.CancellationToken);
        DocumentVersion latest = await db.DocumentVersions
            .SingleAsync(v => v.DocumentId == seeded.Id && v.VersionNumber == 5,
                TestContext.Current.CancellationToken);
        final.CurrentVersionId.ShouldBe(latest.Id);
    }

    [Fact]
    public async Task AppendVersionAsync_MissingDocument_ReturnsNull()
    {
        var blobId = Guid.NewGuid();
        StubBlobConfirmation(blobId);

        DocumentVersion? result = await _sut.AppendVersionAsync(
            Guid.NewGuid(), blobId, OwnerId, null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task AppendVersionAsync_TrashedDocument_Throws()
    {
        Document seeded = await SeedDocumentAsync();
        await using (DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            Document tracked = await db.Documents.SingleAsync(d => d.Id == seeded.Id,
                TestContext.Current.CancellationToken);
            tracked.Trash(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var blobId = Guid.NewGuid();
        StubBlobConfirmation(blobId);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.AppendVersionAsync(seeded.Id, blobId, OwnerId, null,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AppendVersionAsync_BlobInvalid_Throws_NoVersionPersisted()
    {
        Document seeded = await SeedDocumentAsync();
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: false, Status: BlobStatus.Rejected,
                VerifiedContentType: null, SizeBytes: null, RejectionReason: "Magic-bytes mismatch"));

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _sut.AppendVersionAsync(seeded.Id, blobId, OwnerId, null,
                TestContext.Current.CancellationToken));

        // Only the original v1 should be present.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        int count = await db.DocumentVersions
            .CountAsync(v => v.DocumentId == seeded.Id, TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task AppendVersionAsync_ConcurrentAppends_ProduceDistinctMonotonicVersionNumbers()
    {
        Document seeded = await SeedDocumentAsync();

        // Stub a fresh blob per parallel invocation. NSubstitute's per-argument stubs are
        // honoured concurrently because each call resolves the matching Returns entry.
        Guid[] blobIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        foreach (Guid b in blobIds)
        {
            StubBlobConfirmation(b);
        }

        Task<DocumentVersion?>[] tasks = blobIds
            .Select(b => _sut.AppendVersionAsync(seeded.Id, b, OwnerId, null,
                TestContext.Current.CancellationToken))
            .ToArray();

        DocumentVersion?[] results = await Task.WhenAll(tasks);

        results.ShouldAllBe(v => v != null);
        // Distinct version numbers awarded — the unique-index + retry loop guarantees no duplicates.
        int[] numbers = [.. results.Select(v => v!.VersionNumber).OrderBy(n => n)];
        numbers.Distinct().Count().ShouldBe(blobIds.Length);
        numbers.ShouldAllBe(n => n >= 2);
        numbers.Max().ShouldBe(1 + blobIds.Length);

        // Document.CurrentVersionId points at one of the created versions (whichever
        // wrote last under the optimistic concurrency token wins last-write).
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        Document final = await db.Documents.SingleAsync(d => d.Id == seeded.Id,
            TestContext.Current.CancellationToken);
        Guid[] versionIds = [.. results.Select(v => v!.Id)];
        versionIds.ShouldContain(final.CurrentVersionId!.Value);

        // No duplicate (DocumentId, VersionNumber) tuples — the unique index would have
        // thrown DbUpdateException; we'd see a thrown task above otherwise.
        List<int> persistedNumbers = await db.DocumentVersions
            .Where(v => v.DocumentId == seeded.Id)
            .Select(v => v.VersionNumber)
            .ToListAsync(TestContext.Current.CancellationToken);
        persistedNumbers.Count.ShouldBe(1 + blobIds.Length);
        persistedNumbers.Distinct().Count().ShouldBe(persistedNumbers.Count);
    }

    // -------------------------------------------------------------------------
    // F4.2 — ListVersionsAsync (paged history)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListVersionsAsync_AfterFinalize_ReturnsSingleV1_FlaggedAsCurrent()
    {
        Document seeded = await SeedDocumentAsync();

        DocumentVersionPage? page = await _sut.ListVersionsAsync(
            seeded.Id, skip: 0, take: 50, TestContext.Current.CancellationToken);

        page.ShouldNotBeNull();
        page.TotalCount.ShouldBe(1);
        page.CurrentVersionId.ShouldBe(seeded.CurrentVersionId);
        DocumentVersion only = page.Versions.ShouldHaveSingleItem();
        only.VersionNumber.ShouldBe(1);
        only.Id.ShouldBe(seeded.CurrentVersionId!.Value);
    }

    [Fact]
    public async Task ListVersionsAsync_MultipleVersions_OrderedDescending()
    {
        Document seeded = await SeedDocumentAsync();
        for (int i = 2; i <= 4; i++)
        {
            var blobId = Guid.NewGuid();
            StubBlobConfirmation(blobId);
            await _sut.AppendVersionAsync(seeded.Id, blobId, OwnerId, $"v{i}",
                TestContext.Current.CancellationToken);
        }

        DocumentVersionPage? page = await _sut.ListVersionsAsync(
            seeded.Id, skip: 0, take: 50, TestContext.Current.CancellationToken);

        page.ShouldNotBeNull();
        page.TotalCount.ShouldBe(4);
        int[] numbers = [.. page.Versions.Select(v => v.VersionNumber)];
        numbers.ShouldBe([4, 3, 2, 1]);
        // Latest version is the current pointer.
        page.CurrentVersionId.ShouldBe(page.Versions[0].Id);
    }

    [Fact]
    public async Task ListVersionsAsync_PagingSlicesResultSet()
    {
        Document seeded = await SeedDocumentAsync();
        for (int i = 2; i <= 5; i++)
        {
            var blobId = Guid.NewGuid();
            StubBlobConfirmation(blobId);
            await _sut.AppendVersionAsync(seeded.Id, blobId, OwnerId, null,
                TestContext.Current.CancellationToken);
        }

        DocumentVersionPage? firstPage = await _sut.ListVersionsAsync(
            seeded.Id, skip: 0, take: 2, TestContext.Current.CancellationToken);
        DocumentVersionPage? secondPage = await _sut.ListVersionsAsync(
            seeded.Id, skip: 2, take: 2, TestContext.Current.CancellationToken);
        DocumentVersionPage? thirdPage = await _sut.ListVersionsAsync(
            seeded.Id, skip: 4, take: 2, TestContext.Current.CancellationToken);

        firstPage.ShouldNotBeNull();
        firstPage.TotalCount.ShouldBe(5);
        firstPage.Versions.Select(v => v.VersionNumber).ShouldBe([5, 4]);

        secondPage.ShouldNotBeNull();
        secondPage.Versions.Select(v => v.VersionNumber).ShouldBe([3, 2]);

        thirdPage.ShouldNotBeNull();
        thirdPage.Versions.Select(v => v.VersionNumber).ShouldBe([1]);
    }

    [Fact]
    public async Task ListVersionsAsync_MissingDocument_ReturnsNull()
    {
        DocumentVersionPage? page = await _sut.ListVersionsAsync(
            Guid.NewGuid(), skip: 0, take: 50, TestContext.Current.CancellationToken);

        page.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    public async Task ListVersionsAsync_InvalidPaging_Throws(int skip, int take)
    {
        Document seeded = await SeedDocumentAsync();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await _sut.ListVersionsAsync(seeded.Id, skip, take,
                TestContext.Current.CancellationToken));
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

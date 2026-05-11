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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL-backed integration test for F17.9's
/// <see cref="DocumentService.ReplaceVersionBlobAsync"/>: confirms that the
/// version's <c>BlobDescriptorId</c> + <c>SizeBytes</c> are swapped atomically,
/// the tenant quota is rebalanced, the old blob is soft-deleted, and the
/// <see cref="DocumentBlobScrubbedEvent"/> is emitted on the local event bus.
/// </summary>
public sealed class ReplaceVersionBlobPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private DocumentService _sut = null!;
    private IBlobStorage _blobStorage = null!;
    private ILocalEventBus _localEventBus = null!;
    private ITenantQuotaService _quotas = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public ReplaceVersionBlobPostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_document_versions, documents_documents, documents_folders RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(options);
        var bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        _blobStorage = Substitute.For<IBlobStorage>();
        _blobStorage.GetDescriptorAsync(
                DocumentService.ContainerName, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => BlobDescriptor.Create(
                (Guid)ci[1], TenantId, DocumentService.ContainerName,
                $"docs/{(Guid)ci[1]:N}",
                new BlobUploadRequest("file.jpg", "image/jpeg", long.MaxValue),
                DateTimeOffset.UtcNow));

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        _localEventBus = Substitute.For<ILocalEventBus>();

        _quotas = Substitute.For<ITenantQuotaService>();
        _quotas.TryReserveAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _sut = new DocumentService(
            _factory, bootstrap, _blobStorage, currentTenant,
            new SimpleGuidGenerator(), clock, _localEventBus, metrics, _quotas);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task ReplaceVersionBlobAsync_swaps_blob_rebalances_quota_and_emits_event()
    {
        Document seeded = await SeedDocumentAsync(sizeBytes: 5000);
        Guid versionId = seeded.CurrentVersionId!.Value;

        // Inspect pre-state.
        Guid oldBlobId;
        long oldSize;
        await using (DocumentsDbContext ctx = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            DocumentVersion v = await ctx.DocumentVersions.SingleAsync(
                x => x.Id == versionId, TestContext.Current.CancellationToken);
            oldBlobId = v.BlobDescriptorId;
            oldSize = v.SizeBytes;
        }
        oldSize.ShouldBe(5000);

        var newBlobId = Guid.NewGuid();
        const long newSize = 4500L;

        DocumentVersion? updated = await _sut.ReplaceVersionBlobAsync(
            versionId, newBlobId, newSize, "gps-strip",
            TestContext.Current.CancellationToken);

        updated.ShouldNotBeNull();
        updated.BlobDescriptorId.ShouldBe(newBlobId);
        updated.SizeBytes.ShouldBe(newSize);

        // Persisted row reflects the swap.
        await using (DocumentsDbContext ctx = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            DocumentVersion persisted = await ctx.DocumentVersions.SingleAsync(
                x => x.Id == versionId, TestContext.Current.CancellationToken);
            persisted.BlobDescriptorId.ShouldBe(newBlobId);
            persisted.SizeBytes.ShouldBe(newSize);
        }

        // Quota: decrement old size, increment new size.
        await _quotas.Received(1).DecrementAsync(TenantId, oldSize, Arg.Any<CancellationToken>());
        await _quotas.Received(1).IncrementAsync(TenantId, newSize, Arg.Any<CancellationToken>());

        // Old blob soft-deleted.
        await _blobStorage.Received(1).DeleteAsync(
            DocumentService.ContainerName, oldBlobId, "gps-strip", Arg.Any<CancellationToken>());

        // Local event published with the right payload.
        await _localEventBus.Received(1).PublishAsync(
            Arg.Is<DocumentBlobScrubbedEvent>(e =>
                e.VersionId == versionId
                && e.OldBlobDescriptorId == oldBlobId
                && e.NewBlobDescriptorId == newBlobId
                && e.OldSizeBytes == oldSize
                && e.NewSizeBytes == newSize
                && e.Reason == "gps-strip"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplaceVersionBlobAsync_returns_null_when_version_missing()
    {
        DocumentVersion? result = await _sut.ReplaceVersionBlobAsync(
            Guid.NewGuid(), Guid.NewGuid(), 100, "gps-strip",
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _quotas.DidNotReceiveWithAnyArgs().DecrementAsync(default, default, TestContext.Current.CancellationToken);
        await _blobStorage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default, default, TestContext.Current.CancellationToken);
    }

    private async Task<Document> SeedDocumentAsync(long sizeBytes)
    {
        var blobId = Guid.NewGuid();
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "image/jpeg",
                SizeBytes: sizeBytes, RejectionReason: null));
        return await _sut.FinalizeUploadAsync(
            blobId, folderId: null, OwnerId, "photo.jpg", null, "v1",
            TestContext.Current.CancellationToken);
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

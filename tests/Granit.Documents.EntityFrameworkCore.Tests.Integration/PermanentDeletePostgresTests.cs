using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.Documents;
using Granit.Documents.Diagnostics;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
using Granit.Documents.Options;
using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Postgres-backed verification of the F8.2 permanent-delete + trash-list flow on top
/// of the real ITenantQuotaService. The IBlobStorage dependency stays substituted —
/// we're pinning the EF Core / quota interaction, not the cloud-side delete.
/// </summary>
public sealed class PermanentDeletePostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private DocumentService _sut = null!;
    private TenantQuotaService _quotas = null!;
    private IBlobStorage _blobStorage = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public PermanentDeletePostgresTests(PostgresFixture postgres) => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<DocumentsDbContext> options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using DocumentsDbContext init = new(options);
        await init.Database.EnsureCreatedAsync();
        await init.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE documents_folders, documents_documents, documents_document_versions, documents_tenant_storage_quotas RESTART IDENTITY CASCADE;");

        _factory = new TestDbContextFactory(options);
        var bootstrap = new DocumentBootstrapService(_factory, new SimpleGuidGenerator());

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(true);
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        IOptions<GranitDocumentsOptions> opts = Microsoft.Extensions.Options.Options.Create(
            new GranitDocumentsOptions { DefaultTenantQuotaBytes = 5L * 1024L * 1024L * 1024L });

        _quotas = new TenantQuotaService(_factory, new SimpleGuidGenerator(), clock, opts);

        _blobStorage = Substitute.For<IBlobStorage>();
        _blobStorage.GetDescriptorAsync(
                DocumentService.ContainerName, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => BlobDescriptor.Create(
                (Guid)ci[1], TenantId, DocumentService.ContainerName,
                $"docs/{(Guid)ci[1]:N}",
                new BlobUploadRequest("f.pdf", "application/pdf", 1_000_000),
                DateTimeOffset.UtcNow));
        _blobStorage.ConfirmUploadAsync(
                DocumentService.ContainerName, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf",
                SizeBytes: 4096, RejectionReason: null));

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        ILocalEventBus localEventBus = Substitute.For<ILocalEventBus>();

        _sut = new DocumentService(
            _factory, bootstrap, _blobStorage, currentTenant,
            new SimpleGuidGenerator(), clock, localEventBus, metrics, _quotas);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task PermanentlyDeleteAsync_TrashedDocument_DeletesBlob_DecrementsQuota_OnPostgres()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Upload (FinalizeUpload reserves+releases through the real quota service) →
        // trash → permanent delete.
        Document doc = await _sut.FinalizeUploadAsync(
            Guid.NewGuid(), folderId: null, OwnerId, "F.pdf", null, null, ct);
        TenantStorageQuota? afterUpload = await _quotas.GetAsync(TenantId, ct);
        afterUpload.ShouldNotBeNull();
        afterUpload.UsageBytes.ShouldBe(4096);

        await _sut.TrashAsync(doc.Id, ct);
        await _sut.PermanentlyDeleteAsync(doc.Id, ct);

        // Quota fully released back to zero.
        TenantStorageQuota? afterDelete = await _quotas.GetAsync(TenantId, ct);
        afterDelete.ShouldNotBeNull();
        afterDelete.UsageBytes.ShouldBe(0);

        // Blob descriptor delete propagated.
        await _blobStorage.Received(1).DeleteAsync(
            DocumentService.ContainerName,
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            ct);

        // Document row stays as a tombstone.
        await using DocumentsDbContext db = await _factory.CreateDbContextAsync(ct);
        Document tombstone = await db.Documents.SingleAsync(d => d.Id == doc.Id, ct);
        tombstone.Status.ShouldBe(DocumentStatus.PermanentlyDeleted);
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

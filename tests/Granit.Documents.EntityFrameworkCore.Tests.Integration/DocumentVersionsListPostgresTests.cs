using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.Documents;
using Granit.Documents.Diagnostics;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Internal;
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
/// PostgreSQL-backed integration test for <see cref="DocumentService.ListVersionsAsync"/>.
/// Confirms that the OFFSET / LIMIT translation produces correct paging under realistic
/// Postgres semantics (the SQLite tests cover the same ordering, but Postgres is the
/// production target and treats <c>ORDER BY ... DESC</c> + integer comparisons through
/// a different planner path).
/// </summary>
public sealed class DocumentVersionsListPostgresTests :
    IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestDbContextFactory _factory = null!;
    private DocumentService _sut = null!;
    private IBlobStorage _blobStorage = null!;
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    public DocumentVersionsListPostgresTests(PostgresFixture postgres) => _postgres = postgres;

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
        // F7.2 quota path requires GetDescriptorAsync to return a blob with
        // MaxAllowedBytes; default to a permissive descriptor so the existing tests don't
        // need to stub it explicitly.
        _blobStorage.GetDescriptorAsync(
                DocumentService.ContainerName, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => BlobDescriptor.Create(
                (Guid)ci[1], TenantId, DocumentService.ContainerName,
                $"docs/{(Guid)ci[1]:N}",
                new BlobUploadRequest("file.pdf", "application/pdf", long.MaxValue),
                DateTimeOffset.UtcNow));

        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(TenantId);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider provider = services.BuildServiceProvider();
        var metrics = new DocumentsMetrics(provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>());

        ILocalEventBus localEventBus = Substitute.For<ILocalEventBus>();

        ITenantQuotaService quotas = Substitute.For<ITenantQuotaService>();
        quotas.TryReserveAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _sut = new DocumentService(
            _factory, bootstrap, _blobStorage, currentTenant,
            new SimpleGuidGenerator(), clock, localEventBus, metrics, quotas);
    }

    public ValueTask DisposeAsync() => default;

    [Fact]
    public async Task ListVersionsAsync_AppendsThenPages_OrderedDescending_OnPostgres()
    {
        // Seed v1 via FinalizeUploadAsync, then append v2..v6.
        Document seeded = await SeedDocumentAsync();
        for (int i = 2; i <= 6; i++)
        {
            var blobId = Guid.NewGuid();
            StubBlobConfirmation(blobId, sizeBytes: i * 100L);
            DocumentVersion? appended = await _sut.AppendVersionAsync(
                seeded.Id, blobId, OwnerId, $"v{i}", TestContext.Current.CancellationToken);
            appended.ShouldNotBeNull();
        }

        // First page: latest 3.
        DocumentVersionPage? firstPage = await _sut.ListVersionsAsync(
            seeded.Id, skip: 0, take: 3, TestContext.Current.CancellationToken);
        firstPage.ShouldNotBeNull();
        firstPage.TotalCount.ShouldBe(6);
        firstPage.Versions.Select(v => v.VersionNumber).ShouldBe([6, 5, 4]);
        firstPage.CurrentVersionId.ShouldBe(firstPage.Versions[0].Id);

        // Second page: oldest 3.
        DocumentVersionPage? secondPage = await _sut.ListVersionsAsync(
            seeded.Id, skip: 3, take: 3, TestContext.Current.CancellationToken);
        secondPage.ShouldNotBeNull();
        secondPage.TotalCount.ShouldBe(6);
        secondPage.Versions.Select(v => v.VersionNumber).ShouldBe([3, 2, 1]);
    }

    private async Task<Document> SeedDocumentAsync()
    {
        var blobId = Guid.NewGuid();
        StubBlobConfirmation(blobId);
        return await _sut.FinalizeUploadAsync(
            blobId, folderId: null, OwnerId, "F.pdf", null, "v1",
            TestContext.Current.CancellationToken);
    }

    private void StubBlobConfirmation(Guid blobId, long sizeBytes = 100)
    {
        _blobStorage
            .ConfirmUploadAsync(DocumentService.ContainerName, blobId, Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(
                IsValid: true, Status: BlobStatus.Valid,
                VerifiedContentType: "application/pdf",
                SizeBytes: sizeBytes, RejectionReason: null));
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

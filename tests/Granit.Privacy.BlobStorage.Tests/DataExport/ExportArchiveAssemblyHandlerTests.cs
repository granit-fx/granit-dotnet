using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Text.Json;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Options;
using Granit.IO;
using Granit.IO.Extensions;
using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.DataExport;

public sealed class ExportArchiveAssemblyHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 4, 19, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RequestedAt = Now.AddMinutes(-2);

    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IBlobStorage _blobStorage = Substitute.For<IBlobStorage>();
    private readonly IExportRequestTrackerWriter _tracker = Substitute.For<IExportRequestTrackerWriter>();
    private readonly FakeHttpMessageHandler _http = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;
    private readonly ITempFileFactory _tempFileFactory;
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(), $"granit-privacy-test-{Guid.CreateVersion7():N}");

    public ExportArchiveAssemblyHandlerTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddGranitTempFiles(o =>
        {
            o.RootDirectory = _tempRoot;
            o.RunJanitor = false;
            o.TenantPartition = false;
        });
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
        _tempFileFactory = _sp.GetRequiredService<ITempFileFactory>();
    }

    public void Dispose()
    {
        _http.Dispose();
        _sp.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); }
            catch (IOException) { /* best-effort */ }
        }
    }

    private ExportArchiveAssemblyHandler CreateHandler(GranitPrivacyOptions? opts = null) =>
        new(
            _blobStorage,
            _tracker,
            new FakeHttpClientFactory(_http),
            Microsoft.Extensions.Options.Options.Create(opts ?? new GranitPrivacyOptions()),
            _timeProvider,
            _metrics,
            _tempFileFactory,
            NullLogger<ExportArchiveAssemblyHandler>.Instance);

    private void SetupFragmentDownload(Guid blobId, string fileName, string contentType, byte[] payload)
    {
        Uri downloadUri = new($"https://s3.example/{blobId}");
        _blobStorage.CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobId,
            Arg.Any<DownloadUrlOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(downloadUri, Now.AddMinutes(15)));

        _blobStorage.GetDescriptorAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobId,
            Arg.Any<CancellationToken>())
            .Returns((BlobDescriptor?)null);

        _http.MapGet(downloadUri, payload, contentType);
        _ = fileName; // Reserved for a future overload that stubs GetDescriptorAsync to return OriginalFileName.
    }

    private Guid SetupArchiveUpload()
    {
        var archiveBlobId = Guid.NewGuid();
        Uri uploadUri = new($"https://s3.example/upload/{archiveBlobId}");
        _blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            Arg.Any<BlobUploadRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedUploadTicket(
                archiveBlobId,
                uploadUri,
                "PUT",
                Now.AddMinutes(15),
                new Dictionary<string, string>()));

        _blobStorage.ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            archiveBlobId,
            Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(true, BlobStatus.Valid, "application/zip", 100, null));

        _http.MapPut(uploadUri);
        return archiveBlobId;
    }

    [Fact]
    public async Task HandleAsync_HappyPath_MarksCompletedAndUploadsZipWithManifest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        var blobB = Guid.NewGuid();
        SetupFragmentDownload(blobA, "identity.json", "application/json", """{"id":"u1"}"""u8.ToArray());
        SetupFragmentDownload(blobB, "audit.json", "application/json", """[{"evt":"login"}]"""u8.ToArray());
        Guid archiveBlobId = SetupArchiveUpload();

        ExportCompletedEto evt = new(
            requestId, userId, $"personal-data-export/{requestId}",
            IsPartial: false,
            MissingProviders: [],
            Fragments:
            [
                new ReceivedFragment("identity", blobA.ToString(), "application/json"),
                new ReceivedFragment("auditing", blobB.ToString(), "application/json"),
            ],
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

        await CreateHandler().HandleAsync(evt, TestContext.Current.CancellationToken);

        await _tracker.Received(1).MarkCompletedAsync(
            requestId,
            ExportRequestState.Completed,
            PrivacyExportContainerNames.ArchiveBlobReferenceId(requestId),
            Arg.Is<IReadOnlyList<string>>(l => l.Count == 0),
            Arg.Any<CancellationToken>());

        await _blobStorage.Received(1).ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            archiveBlobId,
            Arg.Any<CancellationToken>());

        // Verify the assembled ZIP contains both fragments + manifest.json
        byte[] zipBytes = _http.CapturedUploads.Single().Body;
        using MemoryStream ms = new(zipBytes);
        using ZipArchive zip = new(ms, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.Name).ShouldContain("manifest.json");
        zip.Entries.Count.ShouldBe(3);

        ExportManifest manifest = await ReadManifest(zip, TestContext.Current.CancellationToken);

        manifest.RequestId.ShouldBe(requestId);
        manifest.UserId.ShouldBe(userId);
        manifest.Regulation.ShouldBe("EU_GDPR");
        manifest.RequestedAt.ShouldBe(RequestedAt);
        manifest.CompletedAt.ShouldBe(Now);
        manifest.IsPartial.ShouldBeFalse();
        manifest.EmptyProviders.ShouldBeEmpty();
        manifest.Fragments.Count.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_EmptyFragmentSentinel_SkipsDownloadAndRecordsInManifest()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        SetupFragmentDownload(blobA, "identity.json", "application/json", """{"id":"u1"}"""u8.ToArray());
        SetupArchiveUpload();

        string emptySentinel = $"{PrivacyExportContainerNames.EmptyFragmentPrefix}{requestId}";
        ExportCompletedEto evt = new(
            requestId, userId, $"personal-data-export/{requestId}",
            IsPartial: false,
            MissingProviders: [],
            Fragments:
            [
                new ReceivedFragment("identity", blobA.ToString(), "application/json"),
                new ReceivedFragment("notifications", emptySentinel, "application/json"),
            ],
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

        await CreateHandler().HandleAsync(evt, TestContext.Current.CancellationToken);

        // CreateDownloadUrlAsync is only called for the non-empty blob.
        await _blobStorage.Received(1).CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobA,
            Arg.Any<DownloadUrlOptions>(),
            Arg.Any<CancellationToken>());
        await _blobStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Is<Guid>(g => g != blobA),
            Arg.Any<DownloadUrlOptions>(),
            Arg.Any<CancellationToken>());

        byte[] zipBytes = _http.CapturedUploads.Single().Body;
        using ZipArchive zip = new(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        ExportManifest manifest = await ReadManifest(zip, TestContext.Current.CancellationToken);
        manifest.EmptyProviders.ShouldBe(["notifications"]);
        manifest.Fragments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HandleAsync_PartialExport_MarksPartiallyCompleted()
    {
        var requestId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        SetupFragmentDownload(blobA, "identity.json", "application/json", """{"id":"u1"}"""u8.ToArray());
        SetupArchiveUpload();

        ExportCompletedEto evt = new(
            requestId, Guid.NewGuid(), $"personal-data-export/{requestId}",
            IsPartial: true,
            MissingProviders: ["auditing"],
            Fragments: [new ReceivedFragment("identity", blobA.ToString(), "application/json")],
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

        await CreateHandler().HandleAsync(evt, TestContext.Current.CancellationToken);

        await _tracker.Received(1).MarkCompletedAsync(
            requestId,
            ExportRequestState.PartiallyCompleted,
            Arg.Any<Granit.Domain.ValueObjects.BlobReference?>(),
            Arg.Is<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "auditing"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SizeLimitExceeded_MarksSizeLimitExceededAndSkipsUpload()
    {
        var requestId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        // Random bytes don't compress — guarantees the CountingStream trips the 1 MB cap.
        byte[] payload = new byte[10 * 1024 * 1024];
        Random.Shared.NextBytes(payload);
        SetupFragmentDownload(blobA, "identity.json", "application/json", payload);

        ExportCompletedEto evt = new(
            requestId, Guid.NewGuid(), $"personal-data-export/{requestId}",
            IsPartial: false,
            MissingProviders: [],
            Fragments: [new ReceivedFragment("identity", blobA.ToString(), "application/json")],
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

        await CreateHandler(new GranitPrivacyOptions { ExportMaxSizeMb = 1 })
            .HandleAsync(evt, TestContext.Current.CancellationToken);

        await _tracker.Received(1).MarkCompletedAsync(
            requestId,
            ExportRequestState.SizeLimitExceeded,
            archiveBlobReferenceId: null,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());

        // InitiateUploadAsync is only called for the final archive upload (never reached here).
        await _blobStorage.DidNotReceive().InitiateUploadAsync(
            Arg.Any<string>(), Arg.Any<BlobUploadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UsesConfiguredDownloadTtl()
    {
        var requestId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        SetupFragmentDownload(blobA, "identity.json", "application/json", """{"id":"u1"}"""u8.ToArray());
        SetupArchiveUpload();

        ExportCompletedEto evt = new(
            requestId, Guid.NewGuid(), $"personal-data-export/{requestId}",
            IsPartial: false,
            MissingProviders: [],
            Fragments: [new ReceivedFragment("identity", blobA.ToString(), "application/json")],
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

        await CreateHandler(new GranitPrivacyOptions { ArchiveAssemblyDownloadUrlExpiryMinutes = 22 })
            .HandleAsync(evt, TestContext.Current.CancellationToken);

        await _blobStorage.Received(1).CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobA,
            Arg.Is<DownloadUrlOptions>(o => o.Expiry == TimeSpan.FromMinutes(22)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DeletesTempFileAfterSuccess()
    {
        var requestId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        SetupFragmentDownload(blobA, "identity.json", "application/json", """{"id":"u1"}"""u8.ToArray());
        SetupArchiveUpload();

        ExportCompletedEto evt = new(
            requestId, Guid.NewGuid(), $"personal-data-export/{requestId}",
            IsPartial: false,
            MissingProviders: [],
            Fragments: [new ReceivedFragment("identity", blobA.ToString(), "application/json")],
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

        string expectedTempPath = Path.Combine(Path.GetTempPath(), $"granit-privacy-export-{requestId}.zip");

        await CreateHandler().HandleAsync(evt, TestContext.Current.CancellationToken);

        File.Exists(expectedTempPath).ShouldBeFalse();
    }

    private static async Task<ExportManifest> ReadManifest(ZipArchive zip, CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = zip.Entries.Single(e => e.Name == "manifest.json");
        await using Stream stream = entry.Open();
        return (await JsonSerializer.DeserializeAsync<ExportManifest>(
            stream,
            WebJsonOptions,
            cancellationToken))!;
    }
}

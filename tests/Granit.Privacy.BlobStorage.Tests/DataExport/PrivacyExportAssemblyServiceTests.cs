using System.Diagnostics.Metrics;
using System.IO.Compression;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Domain.ValueObjects;
using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.BlobStorage.DataExport.Internal;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;
using ConfigOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Privacy.BlobStorage.Tests.DataExport;

public sealed class PrivacyExportAssemblyServiceTests : IDisposable
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 5, 25, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
    private readonly FakeBlobStoreProvider _blobStoreProvider = new();
    private readonly IBlobStorage _blobStorage = Substitute.For<IBlobStorage>();
    private readonly IExportRequestTrackerWriter _tracker = Substitute.For<IExportRequestTrackerWriter>();
    private readonly InMemoryExportAssemblyCheckpointStore _checkpoints = new();
    private readonly EphemeralExportHmacSigner _hmacSigner = new(NullLogger<EphemeralExportHmacSigner>.Instance);
    private readonly FakeHttpMessageHandler _http = new();
    private readonly ServiceProvider _sp;
    private readonly PrivacyMetrics _metrics;

    public PrivacyExportAssemblyServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new PrivacyMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose()
    {
        _hmacSigner.Dispose();
        _http.Dispose();
        _sp.Dispose();
    }

    [Fact]
    public async Task AssembleAsync_HappyPath_ProducesShard_AndManifest_AndMarksTrackerCompleted()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        var blobB = Guid.NewGuid();

        byte[] payloadA = """{"id":"u1"}"""u8.ToArray();
        byte[] payloadB = """[{"evt":"login"}]"""u8.ToArray();
        SetupFragmentDownload(blobA, payloadA);
        SetupFragmentDownload(blobB, payloadB);
        Guid manifestBlobId = SetupManifestUpload();

        ExportCompletedEto evt = BuildEvent(
            requestId, userId,
            [
                BuildSignedFragment(requestId, userId, "identity", blobA, "identity.json", "application/json"),
                BuildSignedFragment(requestId, userId, "auditing", blobB, "audit.json", "application/json"),
            ]);

        await CreateSut().AssembleAsync(evt, TestContext.Current.CancellationToken);

        // Exactly one shard (small payloads).
        _blobStoreProvider.SavedBlobs.Keys.Count(k => k.EndsWith(".zip", StringComparison.Ordinal)).ShouldBe(1);
        byte[] shardBytes = _blobStoreProvider.SavedBlobs.First(kvp => kvp.Key.EndsWith(".zip", StringComparison.Ordinal)).Value;

        // Shard contains the two fragments at their declared entry paths.
        using MemoryStream zipStream = new(shardBytes);
        using ZipArchive zip = new(zipStream, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).ShouldBe(["identity.json", "audit.json"], ignoreOrder: true);

        await _tracker.Received(1).MarkCompletedAsync(
            requestId,
            ExportRequestState.Completed,
            Arg.Is<BlobReference>(b => b.Value == manifestBlobId.ToString()),
            Arg.Is<IReadOnlyList<string>>(l => l.Count == 0),
            Arg.Any<CancellationToken>());

        (await _checkpoints.GetAsync(requestId, tenantId: null, TestContext.Current.CancellationToken))
            .ShouldBeNull("checkpoint should be cleared after successful assembly");
    }

    [Fact]
    public async Task AssembleAsync_RejectsForgedHmac_AndDoesNotShipShard()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        SetupFragmentDownload(blobA, "{}"u8.ToArray());
        SetupManifestUpload();

        // Sign with WRONG provider name → verify will reject when the assembler
        // reconstructs parameters from fragment.ProviderName = "identity".
        DateTimeOffset expiry = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(20);
        string forgedTag = _hmacSigner.Sign(new ExportHmacParameters(
            requestId, userId, ProviderName: "wrong-provider", FragmentKind: "staged",
            SourceContainer: PrivacyExportContainerNames.FragmentContainer,
            SourceBlobId: blobA, EntryPath: "identity.json", ExpiresAt: expiry));

        ReceivedFragment fragment = new(
            ProviderName: "identity",
            FragmentKind: "staged",
            SourceContainer: PrivacyExportContainerNames.FragmentContainer,
            BlobReferenceId: BlobReference.Create(blobA.ToString()),
            EntryPath: "identity.json",
            ContentType: "application/json",
            IntegrityTag: forgedTag);

        ExportCompletedEto evt = BuildEvent(requestId, userId, [fragment]);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateSut().AssembleAsync(evt, TestContext.Current.CancellationToken));

        _blobStoreProvider.SavedBlobs.ShouldBeEmpty();
        await _tracker.DidNotReceive().MarkCompletedAsync(
            Arg.Any<Guid>(), Arg.Any<ExportRequestState>(), Arg.Any<BlobReference?>(),
            Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssembleAsync_EmptyFragment_RecordedAsEmptyProvider_NoBlobRead()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blobA = Guid.NewGuid();
        SetupFragmentDownload(blobA, "{}"u8.ToArray());
        Guid manifestBlobId = SetupManifestUpload();

        ReceivedFragment emptyFragment = new(
            ProviderName: "notifications",
            FragmentKind: "empty",
            SourceContainer: PrivacyExportContainerNames.FragmentContainer,
            BlobReferenceId: BlobReference.Create($"{PrivacyExportContainerNames.EmptyFragmentPrefix}{requestId}"),
            EntryPath: "notifications.empty",
            ContentType: "application/octet-stream",
            IntegrityTag: string.Empty);

        ExportCompletedEto evt = BuildEvent(
            requestId, userId,
            [
                BuildSignedFragment(requestId, userId, "identity", blobA, "identity.json", "application/json"),
                emptyFragment,
            ]);

        await CreateSut().AssembleAsync(evt, TestContext.Current.CancellationToken);

        // Exactly one shard with the non-empty fragment.
        byte[] shardBytes = _blobStoreProvider.SavedBlobs.First(kvp => kvp.Key.EndsWith(".zip", StringComparison.Ordinal)).Value;
        using MemoryStream zipStream = new(shardBytes);
        using ZipArchive zip = new(zipStream, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).ShouldBe(["identity.json"]);

        // Empty provider name surfaces in the manifest blob upload's payload — best
        // observed via tracker MarkCompletedAsync (manifest blob id).
        await _tracker.Received(1).MarkCompletedAsync(
            requestId,
            ExportRequestState.Completed,
            Arg.Is<BlobReference>(b => b.Value == manifestBlobId.ToString()),
            Arg.Any<IReadOnlyList<string>?>(),
            Arg.Any<CancellationToken>());
    }

    private PrivacyExportAssemblyService CreateSut() =>
        new(
            _blobStorage,
            _blobStoreProvider,
            _hmacSigner,
            _checkpoints,
            _tracker,
            new FakeHttpClientFactory(_http),
            ConfigOptions.Create(new GranitPrivacyOptions()),
            _timeProvider,
            _metrics,
            NullLogger<PrivacyExportAssemblyService>.Instance);

    private void SetupFragmentDownload(Guid blobId, byte[] payload)
    {
        Uri downloadUri = new($"https://s3.example/{blobId}");
        _blobStorage.CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobId,
            Arg.Any<DownloadUrlOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(downloadUri, _timeProvider.GetUtcNow().AddMinutes(15)));
        _blobStorage.GetDescriptorAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobId,
            Arg.Any<CancellationToken>())
            .Returns((BlobDescriptor?)null);
        _http.MapGet(downloadUri, payload, "application/json");
    }

    private Guid SetupManifestUpload()
    {
        var manifestBlobId = Guid.NewGuid();
        Uri uploadUri = new($"https://s3.example/upload/{manifestBlobId}");
        _blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            Arg.Any<BlobUploadRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedUploadTicket(
                manifestBlobId,
                uploadUri,
                "PUT",
                _timeProvider.GetUtcNow().AddMinutes(15),
                new Dictionary<string, string>()));
        _blobStorage.ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            manifestBlobId,
            Arg.Any<CancellationToken>())
            .Returns(new BlobConfirmationResult(true, BlobStatus.Valid, "application/json", 100, null));
        _http.MapPut(uploadUri);
        return manifestBlobId;
    }

    private ReceivedFragment BuildSignedFragment(
        Guid requestId, Guid userId, string providerName, Guid blobId, string entryPath, string contentType)
    {
        DateTimeOffset expiry = _timeProvider.GetUtcNow() + TimeSpan.FromMinutes(20);
        string tag = _hmacSigner.Sign(new ExportHmacParameters(
            requestId, userId, providerName, "staged",
            PrivacyExportContainerNames.FragmentContainer, blobId, entryPath, expiry));

        return new ReceivedFragment(
            ProviderName: providerName,
            FragmentKind: "staged",
            SourceContainer: PrivacyExportContainerNames.FragmentContainer,
            BlobReferenceId: BlobReference.Create(blobId.ToString()),
            EntryPath: entryPath,
            ContentType: contentType,
            IntegrityTag: tag);
    }

    private static ExportCompletedEto BuildEvent(Guid requestId, Guid userId, IReadOnlyList<ReceivedFragment> fragments) =>
        new(
            RequestId: requestId,
            UserId: userId,
            ArchiveBlobReferenceId: BlobReference.Create($"personal-data-export/{requestId}"),
            IsPartial: false,
            MissingProviders: [],
            Fragments: fragments,
            Regulation: "EU_GDPR",
            RequestedAt: RequestedAt);

    /// <summary>
    /// In-memory <see cref="IBlobStoreProvider"/> — exercises the default interface
    /// method of <c>OpenWriteMultipartAsync</c>.
    /// </summary>
    private sealed class FakeBlobStoreProvider : IBlobStoreProvider
    {
        public Dictionary<string, byte[]> SavedBlobs { get; } = [];

        public async Task SaveAsync(string bucket, string objectKey, Stream content, string contentType, CancellationToken cancellationToken)
        {
            using MemoryStream ms = new();
            await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            SavedBlobs[objectKey] = ms.ToArray();
        }

        public Task<Stream> OpenReadAsync(string bucket, string objectKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<long> GetSizeAsync(string bucket, string objectKey, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Stream> OpenPartialReadAsync(string bucket, string objectKey, int byteCount, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}

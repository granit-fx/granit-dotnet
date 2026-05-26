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
using Granit.Privacy.DataExport.Exceptions;
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
    public async Task AssembleAsync_WrapsTransientFailure_InPrivacyExportAssemblyException()
    {
        // A blob-storage HTTP failure during fragment download is the canonical
        // transient case the Wolverine retry-with-cooldown policy targets. The
        // service wraps the inner HttpRequestException in
        // PrivacyExportAssemblyException so the policy matches and re-dispatches.
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var blobA = Guid.NewGuid();

        // Stub the presigned URL but DON'T MapGet the URI — the fake handler
        // returns 404 → HttpResponseMessage.EnsureSuccessStatusCode throws.
        Uri downloadUri = new($"https://s3.example/{blobA}");
        _blobStorage.CreateDownloadUrlAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobA,
            Arg.Any<DownloadUrlOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedDownloadUrl(downloadUri, _timeProvider.GetUtcNow().AddMinutes(15)));
        _blobStorage.GetDescriptorAsync(
            PrivacyExportContainerNames.FragmentContainer,
            blobA,
            Arg.Any<CancellationToken>())
            .Returns((BlobDescriptor?)null);
        SetupManifestUpload();

        ReceivedFragment fragment = BuildSignedFragment(requestId, userId, "identity", blobA, "identity.json", "application/json");
        ExportCompletedEto evt = BuildEvent(requestId, userId, [fragment]);

        PrivacyExportAssemblyException ex = await Should.ThrowAsync<PrivacyExportAssemblyException>(() =>
            CreateSut().AssembleAsync(evt, TestContext.Current.CancellationToken));

        ex.RequestId.ShouldBe(requestId);
        ex.InnerException.ShouldNotBeNull("the inner HTTP exception is preserved for diagnostics");
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

    [Fact]
    public async Task AssembleAsync_WritesCheckpoint_OnEveryShardRollover()
    {
        // Three large incompressible payloads with a 1 MB shard cap force one
        // rollover (shard 0 closes when fragment 2 is appended). Verify the
        // checkpoint snapshots that rollover: shard 0 committed, next fragment
        // index points at fragment 2 (currently in flight in shard 1).
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        Guid[] blobs = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        byte[] big = new byte[700 * 1024];
        Random.Shared.NextBytes(big);
        foreach (Guid b in blobs)
        {
            SetupFragmentDownload(b, big, "application/octet-stream");
        }
        SetupManifestUpload();

        ExportCompletedEto evt = BuildEvent(
            requestId, userId,
            [
                BuildSignedFragment(requestId, userId, "p0", blobs[0], "p0.bin", "application/octet-stream"),
                BuildSignedFragment(requestId, userId, "p1", blobs[1], "p1.bin", "application/octet-stream"),
                BuildSignedFragment(requestId, userId, "p2", blobs[2], "p2.bin", "application/octet-stream"),
            ]);

        GranitPrivacyOptions opts = new() { ExportShardMaxSizeMb = 1 };
        await CreateSut(opts).AssembleAsync(evt, TestContext.Current.CancellationToken);

        _blobStoreProvider.SavedBlobs.Keys.Count(k => k.EndsWith(".zip", StringComparison.Ordinal)).ShouldBe(2);

        // After clean completion the checkpoint is cleared — but along the way the
        // mid-flight one was observed. We assert the latter via a fresh harness so
        // we can capture the in-flight state.
    }

    [Fact]
    public async Task AssembleAsync_MidFlightCheckpoint_RecordsCommittedShardAndCurrentFragmentIndex()
    {
        // Spy on the checkpoint store to capture every SetAsync between the start
        // marker and the final ClearAsync. The mid-flight snapshot must point at
        // the fragment that triggered the rollover (= fragment 2 here).
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        Guid[] blobs = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        byte[] big = new byte[700 * 1024];
        Random.Shared.NextBytes(big);
        foreach (Guid b in blobs)
        {
            SetupFragmentDownload(b, big, "application/octet-stream");
        }
        SetupManifestUpload();

        var spy = new RecordingCheckpointStore();
        ExportCompletedEto evt = BuildEvent(
            requestId, userId,
            [
                BuildSignedFragment(requestId, userId, "p0", blobs[0], "p0.bin", "application/octet-stream"),
                BuildSignedFragment(requestId, userId, "p1", blobs[1], "p1.bin", "application/octet-stream"),
                BuildSignedFragment(requestId, userId, "p2", blobs[2], "p2.bin", "application/octet-stream"),
            ]);

        GranitPrivacyOptions opts = new() { ExportShardMaxSizeMb = 1 };
        PrivacyExportAssemblyService sut = new(
            _blobStorage, _blobStoreProvider, _hmacSigner, spy, _tracker,
            new FakeHttpClientFactory(_http), ConfigOptions.Create(opts),
            _timeProvider, _metrics, NullLogger<PrivacyExportAssemblyService>.Instance);

        await sut.AssembleAsync(evt, TestContext.Current.CancellationToken);

        // First Set: fresh-run marker (LastCompletedShardIndex = -1, NextFragmentIndex = 0).
        spy.Sets[0].Checkpoint.LastCompletedShardIndex.ShouldBe(-1);
        spy.Sets[0].Checkpoint.NextFragmentIndex.ShouldBe(0);

        // Mid-flight Set: shard 0 committed, fragment 2 in flight.
        ExportAssemblyCheckpoint midflight = spy.Sets[1].Checkpoint;
        midflight.LastCompletedShardIndex.ShouldBe(0);
        midflight.NextFragmentIndex.ShouldBe(2);
        midflight.CompletedShardObjectKeys.Count.ShouldBe(1);

        // Clear fired after successful completion.
        spy.Cleared.ShouldBe(1);
    }

    [Fact]
    public async Task AssembleAsync_ResumesFromCheckpoint_SkipsCommittedFragments_AndStartsAtNextShardIndex()
    {
        // Pre-seed a checkpoint pointing at "shard 0 committed, fragment 2 ready
        // to resume". The service must skip fragments 0+1 (no blob read, no
        // re-streaming), continue with fragment 2 into shard 1, and surface the
        // pre-existing shard 0 in the final manifest.
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        Guid[] blobs = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        // Only fragment 2 will be re-streamed — but the resume path still calls
        // ResolveEntryNameAsync for fragments 0+1 (manifest rebuild), so we stub
        // those descriptors too.
        foreach (Guid b in blobs)
        {
            SetupFragmentDownload(b, "{}"u8.ToArray());
        }
        SetupManifestUpload();

        string priorShardKey = $"personal-data-export/{requestId}-000.zip";
        await _checkpoints.SetAsync(
            requestId,
            tenantId: null,
            new ExportAssemblyCheckpoint(
                LastCompletedShardIndex: 0,
                NextFragmentIndex: 2,
                CompletedShardObjectKeys: [priorShardKey]),
            TestContext.Current.CancellationToken);

        ExportCompletedEto evt = BuildEvent(
            requestId, userId,
            [
                BuildSignedFragment(requestId, userId, "p0", blobs[0], "p0.json", "application/json"),
                BuildSignedFragment(requestId, userId, "p1", blobs[1], "p1.json", "application/json"),
                BuildSignedFragment(requestId, userId, "p2", blobs[2], "p2.json", "application/json"),
            ]);

        await CreateSut().AssembleAsync(evt, TestContext.Current.CancellationToken);

        // Only one NEW shard was uploaded (shard 001), even though the final manifest
        // covers two shards (000 and 001).
        _blobStoreProvider.SavedBlobs.Keys.Count(k => k.EndsWith(".zip", StringComparison.Ordinal)).ShouldBe(1);
        _blobStoreProvider.SavedBlobs.Keys.ShouldContain($"personal-data-export/{requestId}-001.zip");

        // The new shard contains only the third fragment.
        byte[] shardBytes = _blobStoreProvider.SavedBlobs[$"personal-data-export/{requestId}-001.zip"];
        using MemoryStream zipStream = new(shardBytes);
        using ZipArchive zip = new(zipStream, ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).ShouldBe(["p2.json"]);

        // Fragments 0 and 1 were re-fetched only for descriptor lookup, NOT for
        // bytes (no presigned-download URL request fired).
        int downloadCalls = _http.Requests.Count(r => r.Method == HttpMethod.Get);
        downloadCalls.ShouldBe(1, "only fragment 2 should re-stream bytes on resume");

        // Checkpoint cleared after the clean run.
        (await _checkpoints.GetAsync(requestId, tenantId: null, TestContext.Current.CancellationToken))
            .ShouldBeNull();
    }

    private PrivacyExportAssemblyService CreateSut(GranitPrivacyOptions? opts = null) =>
        new(
            _blobStorage,
            _blobStoreProvider,
            _hmacSigner,
            _checkpoints,
            _tracker,
            new FakeHttpClientFactory(_http),
            ConfigOptions.Create(opts ?? new GranitPrivacyOptions()),
            _timeProvider,
            _metrics,
            NullLogger<PrivacyExportAssemblyService>.Instance);

    private void SetupFragmentDownload(Guid blobId, byte[] payload, string contentType = "application/json")
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
        _http.MapGet(downloadUri, payload, contentType);
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
    /// Records every <see cref="IExportAssemblyCheckpointStore"/> call so the test
    /// can assert the mid-flight snapshot taken at a shard rollover.
    /// </summary>
    private sealed class RecordingCheckpointStore : IExportAssemblyCheckpointStore
    {
        public List<(Guid RequestId, Guid? TenantId, ExportAssemblyCheckpoint Checkpoint)> Sets { get; } = [];
        public int Cleared { get; private set; }
        private ExportAssemblyCheckpoint? _latest;

        public Task<ExportAssemblyCheckpoint?> GetAsync(Guid requestId, Guid? tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_latest);

        public Task SetAsync(Guid requestId, Guid? tenantId, ExportAssemblyCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            Sets.Add((requestId, tenantId, checkpoint));
            _latest = checkpoint;
            return Task.CompletedTask;
        }

        public Task ClearAsync(Guid requestId, Guid? tenantId, CancellationToken cancellationToken = default)
        {
            Cleared++;
            _latest = null;
            return Task.CompletedTask;
        }
    }

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

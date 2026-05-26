using System.Text;
using System.Text.Json;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Internal;
using Granit.Domain.ValueObjects;
using Granit.Privacy.BlobStorage.DataExport;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Exceptions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests.DataExport;

public sealed class BlobBackedPrivacyExportDownloadResolverTests
{
    private static readonly Guid RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ManifestBlobId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task OpenShardAsync_ReturnsStream_AndPropagatesShardObjectKeyFromManifest()
    {
        Setup setup = BuildSetup(shardCount: 3);

        PrivacyExportDownloadPayload payload = await setup.Resolver
            .OpenShardAsync(RequestId, shardIndex: 1, TestContext.Current.CancellationToken);

        payload.ContentType.ShouldBe("application/zip");
        payload.FileName.ShouldBe($"personal-data-export-{RequestId}-001.zip");

        // Each OpenShardAsync re-reads the manifest first (own descriptor lookup),
        // then opens the targeted shard — assert via the bucket+key tuple.
        await setup.Provider.Received(1).OpenReadAsync(
            "gdpr-exports",
            "personal-data-export/abc-001.zip",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenShardAsync_OutOfBounds_Throws()
    {
        Setup setup = BuildSetup(shardCount: 2);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => setup.Resolver.OpenShardAsync(RequestId, shardIndex: 5, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenManifestAsync_ReturnsManifestJsonStream()
    {
        Setup setup = BuildSetup(shardCount: 2);

        PrivacyExportDownloadPayload payload = await setup.Resolver
            .OpenManifestAsync(RequestId, TestContext.Current.CancellationToken);

        payload.ContentType.ShouldBe("application/json");
        payload.FileName.ShouldBe($"personal-data-export-{RequestId}-manifest.json");
    }

    [Fact]
    public async Task ReadManifestSummaryAsync_NoArchiveYet_Throws()
    {
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(RequestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                RequestId,
                UserId,
                ExportRequestState.Pending,
                RequestedAt: DateTimeOffset.UtcNow,
                CompletedAt: null,
                ArchiveBlobReferenceId: null,
                MissingProviders: []));

        BlobBackedPrivacyExportDownloadResolver resolver = new(
            Substitute.For<IBlobStorage>(),
            Substitute.For<IBlobStoreProvider>(),
            tracker);

        await Should.ThrowAsync<PrivacyExportNotReadyException>(
            () => resolver.ReadManifestSummaryAsync(RequestId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadManifestSummaryAsync_DescriptorMissing_Throws()
    {
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(RequestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                RequestId,
                UserId,
                ExportRequestState.Completed,
                RequestedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow,
                ArchiveBlobReferenceId: BlobReference.Create(ManifestBlobId.ToString()),
                MissingProviders: []));

        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.GetDescriptorAsync("gdpr-exports", ManifestBlobId, Arg.Any<CancellationToken>())
            .Returns((BlobDescriptor?)null);

        BlobBackedPrivacyExportDownloadResolver resolver = new(
            blobStorage,
            Substitute.For<IBlobStoreProvider>(),
            tracker);

        await Should.ThrowAsync<PrivacyExportNotReadyException>(
            () => resolver.ReadManifestSummaryAsync(RequestId, TestContext.Current.CancellationToken));
    }

    private static Setup BuildSetup(int shardCount)
    {
        IExportRequestTrackerReader tracker = Substitute.For<IExportRequestTrackerReader>();
        tracker.GetStatusAsync(RequestId, Arg.Any<CancellationToken>())
            .Returns(new ExportRequestStatus(
                RequestId,
                UserId,
                ExportRequestState.Completed,
                RequestedAt: DateTimeOffset.UtcNow,
                CompletedAt: DateTimeOffset.UtcNow,
                ArchiveBlobReferenceId: BlobReference.Create(ManifestBlobId.ToString()),
                MissingProviders: []));

        var descriptor = BlobDescriptor.Create(
            id: ManifestBlobId,
            tenantId: null,
            containerName: "gdpr-exports",
            objectKey: $"personal-data-export/abc-manifest.json",
            request: new BlobUploadRequest(
                FileName: $"personal-data-export-{RequestId}-manifest.json",
                ContentType: "application/json",
                MaxAllowedBytes: 1024),
            createdAt: DateTimeOffset.UtcNow);

        IBlobStorage blobStorage = Substitute.For<IBlobStorage>();
        blobStorage.GetDescriptorAsync("gdpr-exports", ManifestBlobId, Arg.Any<CancellationToken>())
            .Returns(descriptor);

        // Re-emit a fresh stream for each manifest read so .NET doesn't trip over
        // a disposed stream when the resolver opens the manifest twice (once for
        // descriptor + summary, again for the shard lookup).
        IBlobStoreProvider provider = Substitute.For<IBlobStoreProvider>();
        provider.OpenReadAsync("gdpr-exports", descriptor.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(BuildManifestJson(shardCount))));

        // Each shard read returns an empty stream — content doesn't matter for the
        // tests, only the bucket+objectKey routing.
        for (int i = 0; i < shardCount; i++)
        {
            string objectKey = $"personal-data-export/abc-{i:D3}.zip";
            provider.OpenReadAsync("gdpr-exports", objectKey, Arg.Any<CancellationToken>())
                .Returns(_ => new MemoryStream([]));
        }

        return new Setup(new BlobBackedPrivacyExportDownloadResolver(blobStorage, provider, tracker), provider);
    }

    private static string BuildManifestJson(int shardCount)
    {
        var shards = Enumerable.Range(0, shardCount).Select(i => new
        {
            index = i,
            objectKey = $"personal-data-export/abc-{i:D3}.zip",
            compressedSizeBytes = 1024 * (i + 1),
            sha256 = "00".PadRight(64, '0'),
        });
        // Signed-envelope shape: { payload: { shards: [...] }, integrityTag: "..." }
        return JsonSerializer.Serialize(new
        {
            payload = new { shards },
            integrityTag = "v1:stub",
        });
    }

    private sealed record Setup(BlobBackedPrivacyExportDownloadResolver Resolver, IBlobStoreProvider Provider);
}

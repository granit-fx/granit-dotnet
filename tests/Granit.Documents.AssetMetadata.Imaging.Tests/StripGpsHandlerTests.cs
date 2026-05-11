using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Granit.BlobStorage;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Options;
using Granit.Documents;
using Granit.Documents.AssetMetadata.Imaging.Internal;
using Granit.Documents.AssetMetadata.Options;
using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Guids;
using ImageMagick;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.AssetMetadata.Imaging.Tests;

/// <summary>
/// Integration-style tests for the F17.9 GPS scrub handler. Wires a real
/// <see cref="JpegGpsScrubber"/> + <see cref="StripGpsHandler"/> against an
/// in-memory <see cref="IBlobStorage"/> fake and a substituted
/// <see cref="IDocumentService"/>. Covers the issue's acceptance criterion:
/// upload a JPEG with GPS → the scrubbed blob becomes the version's new
/// <see cref="DocumentVersion.BlobDescriptorId"/> and the bytes carry no GPS
/// coordinates.
/// </summary>
public sealed class StripGpsHandlerTests
{
    [Fact]
    public async Task Handler_swaps_blob_and_strips_GPS_when_JPEG_has_GPS()
    {
        byte[] jpeg = CreateJpegWithGps(latitude: 48.8566, longitude: 2.3522);

        InMemoryBlobStorage storage = new();
        Guid originalBlobId = await storage.SeedAsync(jpeg, "image/jpeg");

        Guid? capturedNewBlobId = null;
        long? capturedNewSize = null;
        string? capturedReason = null;

        IDocumentService documentService = Substitute.For<IDocumentService>();
        documentService.ReplaceVersionBlobAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedNewBlobId = (Guid)ci[1];
                capturedNewSize = (long)ci[2];
                capturedReason = (string)ci[3];
                return Task.FromResult<DocumentVersion?>(DocumentVersionFactory.Materialise(
                    versionId: (Guid)ci[0],
                    documentId: Guid.NewGuid(),
                    blobId: (Guid)ci[1],
                    sizeBytes: (long)ci[2]));
            });

        StripGpsHandler handler = BuildHandler(storage, documentService, stripGpsOnUpload: true);

        DocumentVersionAddedEvent evt = new(
            DocumentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            VersionId: Guid.NewGuid(),
            VersionNumber: 1,
            BlobDescriptorId: originalBlobId,
            ContentType: "image/jpeg",
            SizeBytes: jpeg.Length,
            UploadedByUserId: Guid.NewGuid());

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        capturedNewBlobId.ShouldNotBeNull();
        capturedNewBlobId.Value.ShouldNotBe(originalBlobId, "the scrub must produce a fresh BlobDescriptorId");
        capturedReason.ShouldBe(StripGpsHandler.ScrubReason);

        byte[] scrubbedBytes = storage.GetBytes(capturedNewBlobId.Value);
        scrubbedBytes.Length.ShouldBe((int)capturedNewSize!.Value);
        AssertNoGpsCoordinates(scrubbedBytes);
    }

    [Fact]
    public async Task Handler_short_circuits_when_StripGpsOnUpload_is_disabled()
    {
        byte[] jpeg = CreateJpegWithGps(latitude: 1.0, longitude: 2.0);
        InMemoryBlobStorage storage = new();
        Guid originalBlobId = await storage.SeedAsync(jpeg, "image/jpeg");

        IDocumentService documentService = Substitute.For<IDocumentService>();

        StripGpsHandler handler = BuildHandler(storage, documentService, stripGpsOnUpload: false);

        DocumentVersionAddedEvent evt = NewEvent(originalBlobId, "image/jpeg", jpeg.Length);
        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await documentService.DidNotReceiveWithAnyArgs().ReplaceVersionBlobAsync(
            default, default, default, default!, TestContext.Current.CancellationToken);
        storage.Count.ShouldBe(1, "no scrubbed copy should be uploaded when the toggle is off");
    }

    [Fact]
    public async Task Handler_short_circuits_for_non_image_content_type()
    {
        InMemoryBlobStorage storage = new();
        Guid originalBlobId = await storage.SeedAsync([1, 2, 3], "application/pdf");

        IDocumentService documentService = Substitute.For<IDocumentService>();
        StripGpsHandler handler = BuildHandler(storage, documentService, stripGpsOnUpload: true);

        DocumentVersionAddedEvent evt = NewEvent(originalBlobId, "application/pdf", 3);
        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await documentService.DidNotReceiveWithAnyArgs().ReplaceVersionBlobAsync(
            default, default, default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handler_short_circuits_when_JPEG_has_no_GPS()
    {
        using var image = new MagickImage(MagickColors.Red, 16u, 16u);
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "Canon");
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        using MemoryStream ms = new();
        image.Write(ms);
        byte[] jpeg = ms.ToArray();

        InMemoryBlobStorage storage = new();
        Guid originalBlobId = await storage.SeedAsync(jpeg, "image/jpeg");

        IDocumentService documentService = Substitute.For<IDocumentService>();
        StripGpsHandler handler = BuildHandler(storage, documentService, stripGpsOnUpload: true);

        DocumentVersionAddedEvent evt = NewEvent(originalBlobId, "image/jpeg", jpeg.Length);
        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await documentService.DidNotReceiveWithAnyArgs().ReplaceVersionBlobAsync(
            default, default, default, default!, TestContext.Current.CancellationToken);
    }

    // -------------------------------------------------------------------------
    // helpers
    // -------------------------------------------------------------------------

    private static StripGpsHandler BuildHandler(
        InMemoryBlobStorage storage,
        IDocumentService documentService,
        bool stripGpsOnUpload)
    {
        IHttpClientFactory factory = new InMemoryHttpClientFactory(storage);
        IGuidGenerator guids = Substitute.For<IGuidGenerator>();
        guids.Create().Returns(_ => Guid.NewGuid());
        IOptions<GranitAssetMetadataOptions> options = Microsoft.Extensions.Options.Options.Create(
            new GranitAssetMetadataOptions { StripGpsOnUpload = stripGpsOnUpload });
        return new StripGpsHandler(
            storage,
            documentService,
            factory,
            guids,
            options,
            NullLogger<StripGpsHandler>.Instance);
    }

    private static DocumentVersionAddedEvent NewEvent(Guid blobId, string ct, long size) =>
        new(
            DocumentId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            VersionId: Guid.NewGuid(),
            VersionNumber: 1,
            BlobDescriptorId: blobId,
            ContentType: ct,
            SizeBytes: size,
            UploadedByUserId: Guid.NewGuid());

    private static void AssertNoGpsCoordinates(byte[] bytes)
    {
        IReadOnlyList<MetadataExtractor.Directory> dirs =
            ImageMetadataReader.ReadMetadata(new MemoryStream(bytes));
        foreach (MetadataExtractor.Directory dir in dirs)
        {
            if (dir is GpsDirectory gps)
            {
                foreach (Tag tag in gps.Tags)
                {
                    tag.Name.ShouldNotContain("Latitude", Case.Sensitive);
                    tag.Name.ShouldNotContain("Longitude", Case.Sensitive);
                    tag.Name.ShouldNotContain("Altitude", Case.Sensitive);
                }
            }
        }
    }

    private static byte[] CreateJpegWithGps(double latitude, double longitude)
    {
        using var image = new MagickImage(MagickColors.Blue, 64u, 64u);
        var exif = new ExifProfile();
        ImageMagick.Rational[] lat = ToDms(Math.Abs(latitude));
        ImageMagick.Rational[] lon = ToDms(Math.Abs(longitude));
        exif.SetValue(ExifTag.GPSLatitudeRef, latitude >= 0 ? "N" : "S");
        exif.SetValue(ExifTag.GPSLatitude, lat);
        exif.SetValue(ExifTag.GPSLongitudeRef, longitude >= 0 ? "E" : "W");
        exif.SetValue(ExifTag.GPSLongitude, lon);
        image.SetProfile(exif);
        image.Format = MagickFormat.Jpeg;
        using MemoryStream ms = new();
        image.Write(ms);
        return ms.ToArray();
    }

    private static ImageMagick.Rational[] ToDms(double value)
    {
        int deg = (int)Math.Floor(value);
        double rem = (value - deg) * 60;
        int min = (int)Math.Floor(rem);
        double sec = (rem - min) * 60;
        return
        [
            new((uint)deg, 1u),
            new((uint)min, 1u),
            new((uint)Math.Round(sec * 1000), 1000u),
        ];
    }

    // -------------------------------------------------------------------------
    // In-memory IBlobStorage — minimal surface area the handler actually exercises.
    // -------------------------------------------------------------------------

    private sealed class InMemoryBlobStorage : IBlobStorage
    {
        private readonly ConcurrentDictionary<Guid, byte[]> _blobs = new();
        private readonly ConcurrentDictionary<Guid, string> _contentTypes = new();

        public int Count => _blobs.Count;

        public byte[] GetBytes(Guid blobId) => _blobs[blobId];

        public async Task<Guid> SeedAsync(byte[] bytes, string contentType)
        {
            await Task.Yield();
            var id = Guid.NewGuid();
            _blobs[id] = bytes;
            _contentTypes[id] = contentType;
            return id;
        }

        public Task<PresignedUploadTicket> InitiateUploadAsync(
            string containerName, BlobUploadRequest request, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            _contentTypes[id] = request.ContentType;
            _blobs[id] = []; // pending
            return Task.FromResult(new PresignedUploadTicket(
                BlobId: id,
                UploadUrl: new Uri($"http://inmemory/upload/{id:N}"),
                HttpMethod: "PUT",
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15),
                RequiredHeaders: new Dictionary<string, string>()));
        }

        public Task<PresignedDownloadUrl> CreateDownloadUrlAsync(
            string containerName, Guid blobId, DownloadUrlOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (!_blobs.ContainsKey(blobId))
            {
                throw new BlobStorage.Exceptions.BlobNotFoundException(blobId, containerName);
            }
            return Task.FromResult(new PresignedDownloadUrl(
                Url: new Uri($"http://inmemory/download/{blobId:N}"),
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        public Task<BlobDescriptor?> GetDescriptorAsync(
            string containerName, Guid blobId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BlobDescriptor?>(null);

        public Task DeleteAsync(
            string containerName, Guid blobId, string? deletionReason = null, CancellationToken cancellationToken = default)
        {
            _blobs.TryRemove(blobId, out _);
            _contentTypes.TryRemove(blobId, out _);
            return Task.CompletedTask;
        }

        public Task<BlobConfirmationResult> ConfirmUploadAsync(
            string containerName, Guid blobId, CancellationToken cancellationToken = default)
        {
            _contentTypes.TryGetValue(blobId, out string? ct);
            _blobs.TryGetValue(blobId, out byte[]? bytes);
            return Task.FromResult(new BlobConfirmationResult(
                IsValid: true,
                Status: BlobStatus.Valid,
                VerifiedContentType: ct,
                SizeBytes: bytes?.Length ?? 0,
                RejectionReason: null));
        }

        public Task<int> CleanupOrphansAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public void Store(Guid blobId, byte[] bytes) => _blobs[blobId] = bytes;
    }

    private sealed class InMemoryHttpClientFactory(InMemoryBlobStorage storage) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new InMemoryHandler(storage));
    }

    private sealed class InMemoryHandler(InMemoryBlobStorage storage) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            // /download/{id} or /upload/{id}
            string idStr = path.Split('/')[^1];
            var id = Guid.ParseExact(idStr, "N");

            if (request.Method == HttpMethod.Get)
            {
                byte[] bytes = storage.GetBytes(id);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes),
                };
            }

            if (request.Method == HttpMethod.Put)
            {
                byte[] payload = await request.Content!.ReadAsByteArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
                storage.Store(id, payload);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }

    /// <summary>
    /// Reflection-based hack — DocumentVersion.Create is internal and we only need
    /// a stand-in value for the test's expectations; the handler does not inspect
    /// the returned object beyond null/not-null.
    /// </summary>
    private static class DocumentVersionFactory
    {
        public static DocumentVersion Materialise(Guid versionId, Guid documentId, Guid blobId, long sizeBytes)
        {
            // We can't call internal Create; instead, use the parameterless private
            // ctor + private setters via System.Activator + reflection.
            var instance = (DocumentVersion)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(DocumentVersion));
            typeof(DocumentVersion).GetProperty(nameof(DocumentVersion.BlobDescriptorId))!
                .SetValue(instance, blobId);
            typeof(DocumentVersion).GetProperty(nameof(DocumentVersion.SizeBytes))!
                .SetValue(instance, sizeBytes);
            typeof(DocumentVersion).GetProperty(nameof(DocumentVersion.DocumentId))!
                .SetValue(instance, documentId);
            typeof(Granit.Domain.Entity).GetProperty(nameof(Granit.Domain.Entity.Id))!
                .SetValue(instance, versionId);
            return instance;
        }
    }
}

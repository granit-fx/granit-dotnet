using Granit.BlobStorage;
using Granit.Events;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.BlobStorage.Tests.DataExport;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BlobStorage.Tests;

public sealed class PrivacyFragmentUploaderTests : IDisposable
{
    private readonly IBlobStorage _blobStorage = Substitute.For<IBlobStorage>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly FakeHttpMessageHandler _http = new();

    public void Dispose() => _http.Dispose();

    private PrivacyFragmentUploader CreateSut() =>
        new(_blobStorage, new FakeHttpClientFactory(_http), _eventBus,
            NullLogger<PrivacyFragmentUploader>.Instance);

    [Fact]
    public async Task UploadAsync_EmptyPayload_PublishesEmptySentinel_AndSkipsBlobCalls()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        PersonalDataRequestedEto request = new(requestId, userId, DateTimeOffset.UtcNow, "EU_GDPR");

        StubProvider provider = new(payload: ReadOnlyMemory<byte>.Empty);

        await CreateSut().UploadAsync(request, provider, TestContext.Current.CancellationToken);

        await _blobStorage.DidNotReceive().InitiateUploadAsync(
            Arg.Any<string>(), Arg.Any<BlobUploadRequest>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataPreparedEto>(e =>
                e.RequestId == requestId &&
                e.ProviderName == StubProvider.Name &&
                e.BlobReferenceId == $"{PrivacyExportContainerNames.EmptyFragmentPrefix}{requestId}" &&
                e.ContentType == StubProvider.Content),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_WithPayload_RunsPresignedDance_AndPublishesPreparedEto()
    {
        var requestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        PersonalDataRequestedEto request = new(requestId, userId, DateTimeOffset.UtcNow, "EU_GDPR");

        var blobId = Guid.NewGuid();
        Uri uploadUri = new("https://s3.example/upload/fragment");
        _blobStorage.InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            Arg.Any<BlobUploadRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new PresignedUploadTicket(
                blobId, uploadUri, "PUT", DateTimeOffset.UtcNow.AddMinutes(15),
                new Dictionary<string, string> { ["x-amz-server-side-encryption"] = "AES256" }));

        _http.MapPut(uploadUri);

        StubProvider provider = new(payload: new byte[] { 1, 2, 3, 4 });

        await CreateSut().UploadAsync(request, provider, TestContext.Current.CancellationToken);

        await _blobStorage.Received(1).InitiateUploadAsync(
            PrivacyExportContainerNames.FragmentContainer,
            Arg.Is<BlobUploadRequest>(r =>
                r.FileName == StubProvider.FileNameFor(requestId) &&
                r.ContentType == StubProvider.Content &&
                r.MaxAllowedBytes == 4),
            Arg.Any<CancellationToken>());

        _http.CapturedUploads.Count.ShouldBe(1);
        _http.CapturedUploads[0].Url.ShouldBe(uploadUri);
        _http.CapturedUploads[0].Body.ShouldBe([1, 2, 3, 4]);

        await _blobStorage.Received(1).ConfirmUploadAsync(
            PrivacyExportContainerNames.FragmentContainer, blobId, Arg.Any<CancellationToken>());

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<PersonalDataPreparedEto>(e =>
                e.RequestId == requestId &&
                e.ProviderName == StubProvider.Name &&
                e.BlobReferenceId == blobId.ToString() &&
                e.ContentType == StubProvider.Content),
            Arg.Any<CancellationToken>());
    }

    private sealed class StubProvider(ReadOnlyMemory<byte> payload) : IPrivacyDataProvider
    {
        public const string Name = "stub-provider";
        public const string Content = "application/json";

        public static string ProviderName => Name;
        public static string ContentType => Content;
        public static string FileName(Guid requestId) => FileNameFor(requestId);
        public static string FileNameFor(Guid requestId) => $"stub-{requestId}.json";

        public Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(payload);
    }
}

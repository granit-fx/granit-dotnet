using Amazon.S3;
using Amazon.S3.Model;
using Granit.BlobStorage.S3.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests.Internal;

public sealed class S3MultipartWriteStreamTests
{
    private const string Bucket = "gdpr-exports";
    private const string ObjectKey = "tenant-a/personal-data-export-001.zip";
    private const string UploadId = "u-12345";
    private const int FivMb = 5 * 1024 * 1024;

    [Fact]
    public async Task CompleteAsync_SinglePayloadUnderPartThreshold_UploadsOnePart_AndCompletes()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using (S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            await sut.CompleteAsync(TestContext.Current.CancellationToken);
        }

        await s3.Received(1).UploadPartAsync(
            Arg.Is<UploadPartRequest>(r => r.PartNumber == 1 && r.PartSize == 3 && r.IsLastPart),
            Arg.Any<CancellationToken>());

        await s3.Received(1).CompleteMultipartUploadAsync(
            Arg.Is<CompleteMultipartUploadRequest>(r =>
                r.PartETags.Count == 1 && r.PartETags[0].PartNumber == 1 && r.PartETags[0].ETag == "etag-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_AcrossMultipleParts_FlushesMidStream_AndShipsFinalRemainder()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-x");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        // First write fills the buffer to exactly the part threshold → flush as part 1.
        await sut.WriteAsync(new byte[FivMb], TestContext.Current.CancellationToken);

        await s3.Received(1).UploadPartAsync(
            Arg.Is<UploadPartRequest>(r => r.PartNumber == 1 && r.PartSize == FivMb),
            Arg.Any<CancellationToken>());

        // Second small write stays under the threshold and rides Complete as the final part.
        await sut.WriteAsync(new byte[1024], TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await s3.Received(1).UploadPartAsync(
            Arg.Is<UploadPartRequest>(r => r.PartNumber == 2 && r.PartSize == 1024 && r.IsLastPart),
            Arg.Any<CancellationToken>());

        await s3.Received(1).CompleteMultipartUploadAsync(
            Arg.Is<CompleteMultipartUploadRequest>(r => r.PartETags.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbortAsync_CallsAbortMultipartUpload_AndNoComplete()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await s3.Received(1).AbortMultipartUploadAsync(
            Arg.Is<AbortMultipartUploadRequest>(r => r.BucketName == Bucket && r.Key == ObjectKey && r.UploadId == UploadId),
            Arg.Any<CancellationToken>());
        await s3.DidNotReceive().CompleteMultipartUploadAsync(
            Arg.Any<CompleteMultipartUploadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisposeAsync_WithoutCompleteOrAbort_AbortsImplicitly()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using (S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb))
        {
            await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            // No explicit Complete or Abort.
        }

        await s3.Received(1).AbortMultipartUploadAsync(
            Arg.Any<AbortMultipartUploadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_IsIdempotent()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await s3.Received(1).CompleteMultipartUploadAsync(
            Arg.Any<CompleteMultipartUploadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AbortAsync_IsIdempotent()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        await sut.AbortAsync(TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await s3.Received(1).AbortMultipartUploadAsync(
            Arg.Any<AbortMultipartUploadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_AfterAbort_Throws()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CompleteAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_Throws()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_AfterAbort_Throws()
    {
        IAmazonS3 s3 = CreateS3MockReturningETag("etag-1");

        await using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);
        await sut.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
        await sut.AbortAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<ObjectDisposedException>(
            async () => await sut.WriteAsync(new byte[] { 4 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void StreamSurface_RejectsReadsAndSeeks()
    {
        IAmazonS3 s3 = Substitute.For<IAmazonS3>();
        using S3MultipartWriteStream sut = new(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb);

        sut.CanRead.ShouldBeFalse();
        sut.CanSeek.ShouldBeFalse();
        sut.CanWrite.ShouldBeTrue();

        Should.Throw<NotSupportedException>(() => sut.Read(new byte[4], 0, 4));
        Should.Throw<NotSupportedException>(() => sut.Seek(0, SeekOrigin.Begin));
        Should.Throw<NotSupportedException>(() => sut.SetLength(10));
        Should.Throw<NotSupportedException>(() => sut.Position = 0);
    }

    [Fact]
    public void Ctor_RejectsPartSizeBelowS3Minimum()
    {
        IAmazonS3 s3 = Substitute.For<IAmazonS3>();
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new S3MultipartWriteStream(s3, Bucket, ObjectKey, UploadId, partSizeBytes: FivMb - 1));
    }

    private static IAmazonS3 CreateS3MockReturningETag(string etag)
    {
        IAmazonS3 s3 = Substitute.For<IAmazonS3>();
        s3.UploadPartAsync(Arg.Any<UploadPartRequest>(), Arg.Any<CancellationToken>())
            .Returns(new UploadPartResponse { ETag = etag });
        s3.CompleteMultipartUploadAsync(Arg.Any<CompleteMultipartUploadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CompleteMultipartUploadResponse());
        s3.AbortMultipartUploadAsync(Arg.Any<AbortMultipartUploadRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AbortMultipartUploadResponse());
        return s3;
    }
}

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Amazon.S3;
using Amazon.S3.Model;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.BlobStorage.S3.Diagnostics;
using Granit.BlobStorage.S3.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.S3.Internal;

/// <summary>
/// S3 implementation of <see cref="IBlobStoreProvider"/> and <see cref="IPresignedUrlProvider"/>.
/// Uses AWSSDK.S3 with a configurable <see cref="S3BlobOptions.ServiceUrl"/> for S3-compatible providers.
/// </summary>
// Infrastructure adapter over AmazonS3Client. Unit testing requires a live S3-compatible endpoint.
[ExcludeFromCodeCoverage]
internal sealed class S3BlobClient : IBlobStoreProvider, IPresignedUrlProvider, IDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly IClock _clock;
    private readonly Uri? _httpServiceUrl;

    public S3BlobClient(IOptions<S3BlobOptions> options, IClock clock)
    {
        S3BlobOptions opts = options.Value;

        AmazonS3Config config = S3ConfigFactory.Create(opts);

        Amazon.Runtime.BasicAWSCredentials credentials = new(opts.AccessKey, opts.SecretKey);
        _s3 = new AmazonS3Client(credentials, config);
        _clock = clock;

        // See S3PresignedUrlRewriter for why post-rewriting is required on AWSSDK.S3 v4.
        _httpServiceUrl = opts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? new Uri(opts.ServiceUrl)
            : null;
    }

    // ── IPresignedUrlProvider ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<PresignedUploadTicket> GenerateUploadTicketAsync(
        string bucket,
        string objectKey,
        Guid blobId,
        BlobUploadRequest request,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.UploadTicket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageS3ActivitySource.TagContentType, request.ContentType);

        GetPreSignedUrlRequest presignRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = _clock.Now.UtcDateTime.Add(expiry),
            ContentType = request.ContentType,
        };

        // Embed declared content-type, original filename, and any caller metadata as signed
        // x-amz-meta-* headers. The same dictionary feeds RequiredHeaders below so the client
        // echoes exactly what was signed — otherwise MinIO/S3 returns 400 SignatureDoesNotMatch.
        Dictionary<string, string> signedMetadata = S3UploadMetadataBuilder.Build(request);
        foreach (KeyValuePair<string, string> entry in signedMetadata)
        {
            presignRequest.Metadata.Add(entry.Key, entry.Value);
        }

        string uploadUrl = S3PresignedUrlRewriter.ForceScheme(_s3.GetPreSignedURL(presignRequest), _httpServiceUrl);

        Dictionary<string, string> requiredHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = request.ContentType,
        };
        foreach (KeyValuePair<string, string> entry in signedMetadata)
        {
            requiredHeaders[entry.Key] = entry.Value;
        }

        PresignedUploadTicket ticket = new(
            BlobId: blobId,
            UploadUrl: new Uri(uploadUrl),
            HttpMethod: "PUT",
            ExpiresAt: _clock.Now.Add(expiry),
            RequiredHeaders: requiredHeaders);

        return Task.FromResult(ticket);
    }

    /// <inheritdoc/>
    public Task<PresignedDownloadUrl> GenerateDownloadUrlAsync(
        string bucket,
        string objectKey,
        DownloadUrlOptions? options,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.DownloadUrl);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);

        GetPreSignedUrlRequest presignRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = _clock.Now.UtcDateTime.Add(expiry),
        };

        if (!string.IsNullOrEmpty(options?.DownloadFileName))
        {
            presignRequest.ResponseHeaderOverrides.ContentDisposition =
                ContentDispositionHelper.BuildAttachmentHeader(options.DownloadFileName);
        }

        string downloadUrl = S3PresignedUrlRewriter.ForceScheme(_s3.GetPreSignedURL(presignRequest), _httpServiceUrl);

        PresignedDownloadUrl result = new(
            Url: new Uri(downloadUrl),
            ExpiresAt: _clock.Now.Add(expiry));

        return Task.FromResult(result);
    }

    // ── IBlobStoreProvider ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SaveAsync(
        string bucket,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.Save);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageS3ActivitySource.TagContentType, contentType);

        PutObjectRequest putRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
            ContentType = contentType,
            InputStream = content,
        };

        await _s3.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.Read);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);

        GetObjectRequest getRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
        };

        GetObjectResponse response = await _s3.GetObjectAsync(getRequest, cancellationToken).ConfigureAwait(false);
        return response.ResponseStream;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.Delete);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);

        DeleteObjectRequest deleteRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
        };

        await _s3.DeleteObjectAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> GetSizeAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.GetSize);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);

        GetObjectMetadataRequest metadataRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
        };

        GetObjectMetadataResponse response = await _s3.GetObjectMetadataAsync(metadataRequest, cancellationToken).ConfigureAwait(false);
        return response.ContentLength;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenPartialReadAsync(
        string bucket,
        string objectKey,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageS3ActivitySource.Source.StartActivity(BlobStorageS3ActivitySource.PartialStream);
        activity?.SetTag(BlobStorageS3ActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageS3ActivitySource.TagObjectKey, objectKey);

        GetObjectRequest rangeRequest = new()
        {
            BucketName = bucket,
            Key = objectKey,
            ByteRange = new ByteRange(0, byteCount - 1),
        };

        GetObjectResponse response = await _s3.GetObjectAsync(rangeRequest, cancellationToken).ConfigureAwait(false);
        return response.ResponseStream;
    }

    /// <inheritdoc/>
    public void Dispose() => _s3.Dispose();
}

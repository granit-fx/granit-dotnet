using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Granit.BlobStorage.GoogleCloud.Diagnostics;
using Granit.BlobStorage.GoogleCloud.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;
using Object = Google.Apis.Storage.v1.Data.Object;

namespace Granit.BlobStorage.GoogleCloud.Internal;

/// <summary>
/// Google Cloud Storage implementation of <see cref="IBlobStoreProvider"/> and <see cref="IPresignedUrlProvider"/>.
/// Uses Google.Cloud.Storage.V1 with configurable authentication (ADC or service account key).
/// </summary>
// Infrastructure adapter over StorageClient. Unit testing requires a live GCS endpoint.
[ExcludeFromCodeCoverage]
internal sealed class GoogleCloudBlobClient : IBlobStoreProvider, IPresignedUrlProvider, IDisposable
{
    private readonly StorageClient _storage;
    private readonly UrlSigner _urlSigner;
    private readonly IClock _clock;
    private readonly GcsResumableUploadOperations _resumableUploadOperations;

    public GoogleCloudBlobClient(IOptions<GoogleCloudStorageOptions> options, IClock clock)
    {
        GoogleCloudStorageOptions opts = options.Value;
        _clock = clock;

        if (!string.IsNullOrEmpty(opts.CredentialFilePath))
        {
            ServiceAccountCredential serviceCredential = CredentialFactory.FromFile<ServiceAccountCredential>(opts.CredentialFilePath);
            _storage = StorageClient.Create(serviceCredential.ToGoogleCredential());
            _urlSigner = UrlSigner.FromCredential(serviceCredential);
        }
        else
        {
            // Application Default Credentials (Workload Identity on GKE, metadata server on GCE/Cloud Run)
            var credential = GoogleCredential.GetApplicationDefault();
            _storage = StorageClient.Create(credential);

            if (credential.UnderlyingCredential is ServiceAccountCredential saCred)
            {
                _urlSigner = UrlSigner.FromCredential(saCred);
            }
            else
            {
                // IAM-based signing for non-SA credentials (Workload Identity, metadata server)
                _urlSigner = UrlSigner.FromCredential(
                    credential.UnderlyingCredential as ServiceAccountCredential
                    ?? throw new InvalidOperationException(
                        "Unable to create UrlSigner from Application Default Credentials. " +
                        "Ensure the service account has the iam.serviceAccounts.signBlob permission, " +
                        "or provide a CredentialFilePath to a service account key file."));
            }
        }

        _resumableUploadOperations = new GcsResumableUploadOperations(_storage);
    }

    // ── IPresignedUrlProvider ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<PresignedUploadTicket> GenerateUploadTicketAsync(
        string bucket,
        string objectKey,
        Guid blobId,
        BlobUploadRequest request,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.UploadTicket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagContentType, request.ContentType);

        UrlSigner.RequestTemplate template = UrlSigner.RequestTemplate
            .FromBucket(bucket)
            .WithObjectName(objectKey)
            .WithHttpMethod(System.Net.Http.HttpMethod.Put)
            .WithContentHeaders(new Dictionary<string, IEnumerable<string>>
            {
                ["Content-Type"] = [request.ContentType],
            });

        UrlSigner.Options signerOptions = UrlSigner.Options
            .FromDuration(expiry)
            .WithSigningVersion(SigningVersion.V4);

        string uploadUrl = await _urlSigner.SignAsync(template, signerOptions, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> requiredHeaders = new()
        {
            ["Content-Type"] = request.ContentType,
        };

        PresignedUploadTicket ticket = new(
            BlobId: blobId,
            UploadUrl: new Uri(uploadUrl),
            HttpMethod: "PUT",
            ExpiresAt: _clock.Now.Add(expiry),
            RequiredHeaders: requiredHeaders);

        return ticket;
    }

    /// <inheritdoc/>
    public async Task<PresignedDownloadUrl> GenerateDownloadUrlAsync(
        string bucket,
        string objectKey,
        DownloadUrlOptions? options,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.DownloadUrl);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);

        UrlSigner.RequestTemplate template = UrlSigner.RequestTemplate
            .FromBucket(bucket)
            .WithObjectName(objectKey)
            .WithHttpMethod(System.Net.Http.HttpMethod.Get);

        if (!string.IsNullOrEmpty(options?.DownloadFileName))
        {
            string disposition = ContentDispositionHelper.BuildAttachmentHeader(options.DownloadFileName);
            template = template.WithQueryParameters(new Dictionary<string, IEnumerable<string>>
            {
                ["response-content-disposition"] = [disposition],
            });
        }

        UrlSigner.Options signerOptions = UrlSigner.Options
            .FromDuration(expiry)
            .WithSigningVersion(SigningVersion.V4);

        string downloadUrl = await _urlSigner.SignAsync(template, signerOptions, cancellationToken).ConfigureAwait(false);

        PresignedDownloadUrl result = new(
            Url: new Uri(downloadUrl),
            ExpiresAt: _clock.Now.Add(expiry));

        return result;
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
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.Save);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagContentType, contentType);

        await _storage.UploadObjectAsync(bucket, objectKey, contentType, content, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.Read);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);

        MemoryStream stream = new();
        await _storage.DownloadObjectAsync(bucket, objectKey, stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.Delete);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);

        await _storage.DeleteObjectAsync(bucket, objectKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> GetSizeAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.GetSize);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);

        Object obj = await _storage.GetObjectAsync(bucket, objectKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        return (long)(obj.Size ?? 0UL);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenPartialReadAsync(
        string bucket,
        string objectKey,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity(BlobStorageGoogleCloudActivitySource.PartialStream);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);

        MemoryStream stream = new();
        await _storage.DownloadObjectAsync(
            bucket,
            objectKey,
            stream,
            new DownloadObjectOptions { Range = new System.Net.Http.Headers.RangeHeaderValue(0, byteCount - 1) },
            cancellationToken).ConfigureAwait(false);
        stream.Position = 0;
        return stream;
    }

    /// <inheritdoc/>
    public async Task<MultipartWriteStream> OpenWriteMultipartAsync(
        string bucket,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(bucket);
        ArgumentException.ThrowIfNullOrEmpty(objectKey);
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        using Activity? activity = BlobStorageGoogleCloudActivitySource.Source.StartActivity("Gcs.InitiateResumableUpload");
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageGoogleCloudActivitySource.TagContentType, contentType);

        Uri sessionUri = await _resumableUploadOperations
            .InitiateSessionAsync(bucket, objectKey, contentType, cancellationToken)
            .ConfigureAwait(false);

        return new GcsResumableMultipartWriteStream(_resumableUploadOperations, sessionUri);
    }

    /// <inheritdoc/>
    public void Dispose() => _storage.Dispose();
}

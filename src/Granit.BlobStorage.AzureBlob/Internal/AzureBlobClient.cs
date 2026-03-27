using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Granit.BlobStorage.AzureBlob.Diagnostics;
using Granit.BlobStorage.AzureBlob.Options;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AzureBlob.Internal;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IBlobStoreProvider"/> and <see cref="IPresignedUrlProvider"/>.
/// Uses SAS tokens for Direct-to-Cloud pre-signed uploads and downloads.
/// </summary>
// Infrastructure adapter over Azure.Storage.Blobs. Unit testing requires Azurite or a live endpoint.
[ExcludeFromCodeCoverage]
internal sealed class AzureBlobClient : IBlobStoreProvider, IPresignedUrlProvider
{
    private readonly BlobServiceClient _serviceClient;
    private readonly IClock _clock;
    private readonly bool _canGenerateSas;

    public AzureBlobClient(IOptions<AzureBlobOptions> options, IClock clock)
    {
        AzureBlobOptions opts = options.Value;
        _clock = clock;

        if (opts.UseManagedIdentity)
        {
            _serviceClient = new BlobServiceClient(opts.ServiceUri, new DefaultAzureCredential());
            // User delegation SAS requires async key fetch — handled in GenerateSas methods.
            _canGenerateSas = false;
        }
        else
        {
            _serviceClient = new BlobServiceClient(opts.ConnectionString);
            _canGenerateSas = _serviceClient.CanGenerateAccountSasUri;
        }
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
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.UploadTicket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContentType, request.ContentType);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        Uri sasUri = await GenerateSasUriAsync(
            blobClient, BlobSasPermissions.Write | BlobSasPermissions.Create, expiry,
            request.ContentType, null, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> requiredHeaders = new()
        {
            ["Content-Type"] = request.ContentType,
            ["x-ms-blob-type"] = "BlockBlob",
        };

        PresignedUploadTicket ticket = new(
            BlobId: blobId,
            UploadUrl: sasUri,
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
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.DownloadUrl);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        string? contentDisposition = !string.IsNullOrEmpty(options?.DownloadFileName)
            ? ContentDispositionHelper.BuildAttachmentHeader(options.DownloadFileName)
            : null;

        Uri sasUri = await GenerateSasUriAsync(
            blobClient, BlobSasPermissions.Read, expiry,
            null, contentDisposition, cancellationToken).ConfigureAwait(false);

        PresignedDownloadUrl result = new(
            Url: sasUri,
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
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.Save);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContentType, contentType);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        BlobUploadOptions uploadOptions = new()
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        };

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.Read);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        return await blobClient.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.Delete);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> GetSizeAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.GetSize);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        BlobProperties properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return properties.ContentLength;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenPartialReadAsync(
        string bucket,
        string objectKey,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageAzureActivitySource.Source.StartActivity(BlobStorageAzureActivitySource.PartialStream);
        activity?.SetTag(BlobStorageAzureActivitySource.TagContainer, bucket);
        activity?.SetTag(BlobStorageAzureActivitySource.TagBlobName, objectKey);

        BlobClient blobClient = _serviceClient
            .GetBlobContainerClient(bucket)
            .GetBlobClient(objectKey);

        BlobOpenReadOptions readOptions = new(allowModifications: false)
        {
            Position = 0,
            BufferSize = byteCount,
        };

        Stream stream = await blobClient.OpenReadAsync(readOptions, cancellationToken).ConfigureAwait(false);

        // Wrap in a length-limited stream to ensure we only read byteCount bytes.
        MemoryStream buffer = new();
        byte[] tempBuffer = new byte[byteCount];
        int bytesRead = await stream.ReadAsync(tempBuffer.AsMemory(0, byteCount), cancellationToken).ConfigureAwait(false);
        await buffer.WriteAsync(tempBuffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        await stream.DisposeAsync().ConfigureAwait(false);

        return buffer;
    }

    // ── SAS generation helpers ────────────────────────────────────────────────

    private async Task<Uri> GenerateSasUriAsync(
        BlobClient blobClient,
        BlobSasPermissions permissions,
        TimeSpan expiry,
        string? contentType,
        string? contentDisposition,
        CancellationToken cancellationToken)
    {
        DateTimeOffset expiresOn = _clock.Now.Add(expiry);

        BlobSasBuilder sasBuilder = new()
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b", // blob-level SAS
            ExpiresOn = expiresOn,
        };

        sasBuilder.SetPermissions(permissions);

        if (contentType is not null)
        {
            sasBuilder.ContentType = contentType;
        }

        if (contentDisposition is not null)
        {
            sasBuilder.ContentDisposition = contentDisposition;
        }

        if (_canGenerateSas)
        {
            // Connection string authentication — use account key for SAS.
            return blobClient.GenerateSasUri(sasBuilder);
        }

        // Managed Identity — use user delegation key.
        Response<UserDelegationKey> delegationKey = await _serviceClient
            .GetUserDelegationKeyAsync(null, expiresOn, cancellationToken)
            .ConfigureAwait(false);

        BlobUriBuilder uriBuilder = new(blobClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(delegationKey.Value, _serviceClient.AccountName),
        };

        return uriBuilder.ToUri();
    }
}

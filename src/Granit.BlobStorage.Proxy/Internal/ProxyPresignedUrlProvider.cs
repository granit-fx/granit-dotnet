using System.Diagnostics;
using Granit.BlobStorage.Internal;
using Granit.BlobStorage.Options;
using Granit.BlobStorage.Proxy.Diagnostics;
using Granit.BlobStorage.Proxy.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Proxy.Internal;

/// <summary>
/// Implements <see cref="IPresignedUrlProvider"/> by generating ephemeral token-based URLs
/// pointing to local proxy endpoints, instead of cloud pre-signed URLs.
/// </summary>
internal sealed class ProxyPresignedUrlProvider(
    IBlobProxyTokenStore tokenStore,
    IOptions<ProxyBlobOptions> proxyOptions,
    IClock clock,
    ICurrentTenant currentTenant) : IPresignedUrlProvider
{
    private ProxyBlobOptions Options => proxyOptions.Value;

    /// <inheritdoc/>
    public async Task<PresignedUploadTicket> GenerateUploadTicketAsync(
        string bucket,
        string objectKey,
        Guid blobId,
        BlobUploadRequest request,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageProxyActivitySource.Source.StartActivity(BlobStorageProxyActivitySource.UploadTicket);
        activity?.SetTag(BlobStorageProxyActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageProxyActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageProxyActivitySource.TagContentType, request.ContentType);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        ProxyTokenEntry entry = new(
            Type: ProxyTokenType.Upload,
            Bucket: bucket,
            ObjectKey: objectKey,
            BlobId: blobId,
            TenantId: tenantId,
            ContentType: request.ContentType,
            MaxBytes: Math.Min(request.MaxAllowedBytes, Options.MaxUploadBytes),
            DownloadFileName: null);

        string token = await tokenStore.CreateAsync(entry, expiry, cancellationToken).ConfigureAwait(false);

        Uri uploadUrl = BuildProxyUrl("upload", token);

        Dictionary<string, string> requiredHeaders = new()
        {
            ["Content-Type"] = request.ContentType,
        };

        return new PresignedUploadTicket(
            BlobId: blobId,
            UploadUrl: uploadUrl,
            HttpMethod: "PUT",
            ExpiresAt: clock.Now.Add(expiry),
            RequiredHeaders: requiredHeaders);
    }

    /// <inheritdoc/>
    public async Task<PresignedDownloadUrl> GenerateDownloadUrlAsync(
        string bucket,
        string objectKey,
        DownloadUrlOptions? options,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageProxyActivitySource.Source.StartActivity(BlobStorageProxyActivitySource.DownloadUrl);
        activity?.SetTag(BlobStorageProxyActivitySource.TagBucket, bucket);
        activity?.SetTag(BlobStorageProxyActivitySource.TagObjectKey, objectKey);

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        ProxyTokenEntry entry = new(
            Type: ProxyTokenType.Download,
            Bucket: bucket,
            ObjectKey: objectKey,
            BlobId: Guid.Empty,
            TenantId: tenantId,
            ContentType: null,
            MaxBytes: null,
            DownloadFileName: options?.DownloadFileName);

        string token = await tokenStore.CreateAsync(entry, expiry, cancellationToken).ConfigureAwait(false);

        Uri downloadUrl = BuildProxyUrl("download", token);

        return new PresignedDownloadUrl(
            Url: downloadUrl,
            ExpiresAt: clock.Now.Add(expiry));
    }

    private Uri BuildProxyUrl(string action, string token)
    {
        string baseUrl = Options.BaseUrl.TrimEnd('/');
        string prefix = Options.RoutePrefix.TrimEnd('/');
        return new Uri($"{baseUrl}{prefix}/{action}/{token}");
    }
}

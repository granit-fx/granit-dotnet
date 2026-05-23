namespace Granit.BlobStorage.S3.Internal;

/// <summary>
/// Rewrites the scheme of presigned URLs emitted by AWSSDK.S3 to match the configured endpoint.
/// </summary>
/// <remarks>
/// AWSSDK.S3 v4's endpoint-rules engine regenerates the scheme as <c>https://</c> on presigned
/// URLs even when <c>ServiceURL</c> is <c>http://</c> and <c>UseHttp=true</c> — a behaviour
/// change from v3. The signature is canonicalised on host only (scheme is not part of
/// <c>SignedHeaders</c>), so rewriting the scheme client-side does not invalidate
/// <c>X-Amz-Signature</c>.
/// </remarks>
internal static class S3PresignedUrlRewriter
{
    /// <summary>
    /// Rewrites <paramref name="presignedUrl"/> to use <c>http</c> and the port of
    /// <paramref name="httpServiceUrl"/>. Returns the input unchanged when
    /// <paramref name="httpServiceUrl"/> is <c>null</c> (configured endpoint is HTTPS).
    /// </summary>
    public static string ForceScheme(string presignedUrl, Uri? httpServiceUrl)
    {
        if (httpServiceUrl is null)
        {
            return presignedUrl;
        }

        UriBuilder builder = new(presignedUrl)
        {
            Scheme = Uri.UriSchemeHttp,
            Port = httpServiceUrl.Port,
        };
        return builder.Uri.ToString();
    }
}

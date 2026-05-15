using Amazon.S3;
using Granit.BlobStorage.S3.Options;

namespace Granit.BlobStorage.S3.Internal;

/// <summary>
/// Builds <see cref="AmazonS3Config"/> instances from <see cref="S3BlobOptions"/>.
/// </summary>
internal static class S3ConfigFactory
{
    // The AWS SDK regenerates the scheme on presigned URLs from AmazonS3Config.UseHttp; ServiceURL
    // alone is ignored. Without this, http://localhost:9000 (MinIO dev) yields https:// upload URLs.
    public static AmazonS3Config Create(S3BlobOptions opts) =>
        new()
        {
            ServiceURL = opts.ServiceUrl,
            ForcePathStyle = opts.ForcePathStyle,
            AuthenticationRegion = opts.Region,
            UseHttp = opts.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
        };
}

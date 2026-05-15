using Amazon.S3;
using Amazon.S3.Model;
using Granit.BlobStorage.S3.Internal;
using Granit.BlobStorage.S3.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.S3.HealthChecks;

/// <summary>
/// Health check that verifies S3-compatible storage connectivity by issuing a
/// <c>ListObjectsV2</c> request (max 1 key) on the configured default bucket.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Bucket accessible → <see cref="HealthCheckResult.Healthy"/></item>
///   <item>Bucket not found or access denied → <see cref="HealthCheckResult.Unhealthy"/></item>
///   <item>Unreachable → <see cref="HealthCheckResult.Unhealthy"/></item>
/// </list>
/// The response never exposes endpoint URLs, bucket names, or credentials.
/// </remarks>
internal sealed class S3HealthCheck(
    IOptions<S3BlobOptions> options) : IHealthCheck
{
    private static readonly TimeSpan s_healthCheckTimeout = TimeSpan.FromSeconds(10);
    private readonly AmazonS3Client _s3 = CreateClient(options.Value);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ListObjectsV2Request request = new()
            {
                BucketName = options.Value.DefaultBucket,
                MaxKeys = 1,
            };

            await _s3.ListObjectsV2Async(request, cancellationToken)
                .WaitAsync(s_healthCheckTimeout, cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return HealthCheckResult.Unhealthy("S3 bucket not found");
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return HealthCheckResult.Unhealthy("S3 access denied");
        }
        catch (Exception ex)
        {
            // Sanitize: never expose endpoint URLs or credentials in the message
            return HealthCheckResult.Unhealthy($"S3 unreachable: {ex.GetType().Name}");
        }
    }

    private static AmazonS3Client CreateClient(S3BlobOptions opts)
    {
        AmazonS3Config config = S3ConfigFactory.Create(opts);
        Amazon.Runtime.BasicAWSCredentials credentials = new(opts.AccessKey, opts.SecretKey);
        return new AmazonS3Client(credentials, config);
    }
}

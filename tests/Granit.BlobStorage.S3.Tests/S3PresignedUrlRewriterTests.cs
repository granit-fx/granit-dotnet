using Granit.BlobStorage.S3.Internal;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests;

/// <summary>
/// Unit tests for <see cref="S3PresignedUrlRewriter"/>.
/// Locks in the post-rewrite that compensates AWSSDK.S3 v4's endpoint-rules engine
/// regenerating <c>https://</c> on presigned URLs for <c>http://</c> MinIO endpoints.
/// </summary>
public sealed class S3PresignedUrlRewriterTests
{
    [Fact]
    public void ForceScheme_HttpsServiceUrl_ReturnsInputUnchanged()
    {
        const string presigned = "https://s3.eu-west-1.amazonaws.com/bucket/key?X-Amz-Signature=abc";

        string result = S3PresignedUrlRewriter.ForceScheme(presigned, httpServiceUrl: null);

        result.ShouldBe(presigned);
    }

    [Fact]
    public void ForceScheme_HttpServiceUrl_RewritesHttpsToHttpAndPreservesPort()
    {
        const string presigned = "https://localhost/showcase/2026/05/blob?X-Amz-Signature=abc";
        Uri serviceUrl = new("http://localhost:9000");

        string result = S3PresignedUrlRewriter.ForceScheme(presigned, serviceUrl);

        Uri uri = new(result);
        uri.Scheme.ShouldBe("http");
        uri.Port.ShouldBe(9000);
        uri.Host.ShouldBe("localhost");
        uri.AbsolutePath.ShouldBe("/showcase/2026/05/blob");
        uri.Query.ShouldBe("?X-Amz-Signature=abc");
    }

    [Fact]
    public void ForceScheme_HttpServiceUrlAlreadyHttp_PreservesPortFromServiceUrl()
    {
        // AWSSDK could emit either scheme; this case proves the rewriter is idempotent on http.
        const string presigned = "http://localhost:9000/bucket/key?X-Amz-Signature=abc";
        Uri serviceUrl = new("http://localhost:9000");

        string result = S3PresignedUrlRewriter.ForceScheme(presigned, serviceUrl);

        Uri uri = new(result);
        uri.Scheme.ShouldBe("http");
        uri.Port.ShouldBe(9000);
    }

    [Fact]
    public void ForceScheme_HttpServiceUrlOnCustomPort_OverwritesPresignedPort()
    {
        // AWS SDK may emit port 443 with https; rewriting to http must restore the service port,
        // not fall back to 80 (UriBuilder's default for the http scheme).
        const string presigned = "https://localhost:443/bucket/key?X-Amz-Signature=abc";
        Uri serviceUrl = new("http://localhost:9123");

        string result = S3PresignedUrlRewriter.ForceScheme(presigned, serviceUrl);

        Uri uri = new(result);
        uri.Scheme.ShouldBe("http");
        uri.Port.ShouldBe(9123);
    }

    [Fact]
    public void ForceScheme_QueryStringIsPreservedVerbatim()
    {
        // Signature must remain bit-identical — host (not scheme) is in SignedHeaders, but the
        // signed query parameters (X-Amz-Signature, X-Amz-SignedHeaders, ...) must not be touched.
        const string query =
            "?X-Amz-Expires=900&X-Amz-Algorithm=AWS4-HMAC-SHA256" +
            "&X-Amz-Credential=minioadmin%2F20260523%2Fus-east-1%2Fs3%2Faws4_request" +
            "&X-Amz-Date=20260523T175245Z&X-Amz-SignedHeaders=content-type%3Bhost" +
            "&X-Amz-Signature=d5e5d942e439c237dbda63c6789824979047579dfe3b3eb0e1a4225bff1f6bcb";
        string presigned = $"https://localhost:9000/showcase/blob{query}";
        Uri serviceUrl = new("http://localhost:9000");

        string result = S3PresignedUrlRewriter.ForceScheme(presigned, serviceUrl);

        new Uri(result).Query.ShouldBe(query);
    }
}

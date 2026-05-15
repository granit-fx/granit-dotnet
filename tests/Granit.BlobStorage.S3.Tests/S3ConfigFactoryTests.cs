using Amazon.S3;
using Granit.BlobStorage.S3.Internal;
using Granit.BlobStorage.S3.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests;

/// <summary>
/// Unit tests for <see cref="S3ConfigFactory"/>.
/// Locks in that <c>UseHttp</c> tracks the <see cref="S3BlobOptions.ServiceUrl"/> scheme —
/// required so AWS SDK presigned URLs preserve <c>http://</c> for MinIO dev endpoints.
/// </summary>
public sealed class S3ConfigFactoryTests
{
    [Theory]
    [InlineData("http://localhost:9000", true)]
    [InlineData("HTTP://LOCALHOST:9000", true)]
    [InlineData("https://s3.eu-west-1.amazonaws.com", false)]
    public void Create_SetsUseHttpFromServiceUrlScheme(string serviceUrl, bool expectedUseHttp)
    {
        S3BlobOptions opts = new()
        {
            ServiceUrl = serviceUrl,
            AccessKey = "k",
            SecretKey = "s",
            Region = "us-east-1",
            DefaultBucket = "b",
            ForcePathStyle = true,
        };

        AmazonS3Config config = S3ConfigFactory.Create(opts);

        config.UseHttp.ShouldBe(expectedUseHttp);
        config.ServiceURL.ShouldStartWith(serviceUrl);
        config.ForcePathStyle.ShouldBeTrue();
        config.AuthenticationRegion.ShouldBe("us-east-1");
    }
}

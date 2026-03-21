using Granit.BlobStorage.S3.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests;

public sealed class S3BlobOptionsTests
{
    [Fact]
    public void DefaultRegion_IsUsEast1()
    {
        S3BlobOptions options = new();

        options.Region.ShouldBe("us-east-1");
    }

    [Fact]
    public void DefaultForcePathStyle_IsTrue()
    {
        S3BlobOptions options = new();

        options.ForcePathStyle.ShouldBeTrue();
    }

    [Fact]
    public void DefaultTenantIsolation_IsPrefix()
    {
        S3BlobOptions options = new();

        options.TenantIsolation.ShouldBe(BlobTenantIsolation.Prefix);
    }

    [Fact]
    public void DefaultServiceUrl_IsEmpty()
    {
        S3BlobOptions options = new();

        options.ServiceUrl.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultAccessKey_IsEmpty()
    {
        S3BlobOptions options = new();

        options.AccessKey.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultSecretKey_IsEmpty()
    {
        S3BlobOptions options = new();

        options.SecretKey.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultBucket_IsEmpty()
    {
        S3BlobOptions options = new();

        options.DefaultBucket.ShouldBeEmpty();
    }

    [Fact]
    public void InheritsFromBlobStorageOptions()
    {
        S3BlobOptions options = new();

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(15));
        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(5));
    }
}

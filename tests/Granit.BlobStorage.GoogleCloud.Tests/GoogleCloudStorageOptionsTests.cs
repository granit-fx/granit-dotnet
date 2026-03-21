using Granit.BlobStorage.GoogleCloud.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.GoogleCloud.Tests;

public sealed class GoogleCloudStorageOptionsTests
{
    [Fact]
    public void DefaultProjectId_IsEmpty()
    {
        GoogleCloudStorageOptions options = new();

        options.ProjectId.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultBucket_IsEmpty()
    {
        GoogleCloudStorageOptions options = new();

        options.DefaultBucket.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultCredentialFilePath_IsNull()
    {
        GoogleCloudStorageOptions options = new();

        options.CredentialFilePath.ShouldBeNull();
    }

    [Fact]
    public void DefaultTenantIsolation_IsPrefix()
    {
        GoogleCloudStorageOptions options = new();

        options.TenantIsolation.ShouldBe(BlobTenantIsolation.Prefix);
    }

    [Fact]
    public void InheritsFromBlobStorageOptions()
    {
        GoogleCloudStorageOptions options = new();

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(15));
        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(5));
    }
}

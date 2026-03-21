using Granit.BlobStorage.AzureBlob.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.AzureBlob.Tests;

public sealed class AzureBlobOptionsTests
{
    [Fact]
    public void DefaultConnectionString_IsEmpty()
    {
        AzureBlobOptions options = new();

        options.ConnectionString.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultContainer_IsEmpty()
    {
        AzureBlobOptions options = new();

        options.DefaultContainer.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultUseManagedIdentity_IsFalse()
    {
        AzureBlobOptions options = new();

        options.UseManagedIdentity.ShouldBeFalse();
    }

    [Fact]
    public void DefaultServiceUri_IsNull()
    {
        AzureBlobOptions options = new();

        options.ServiceUri.ShouldBeNull();
    }

    [Fact]
    public void DefaultTenantIsolation_IsPrefix()
    {
        AzureBlobOptions options = new();

        options.TenantIsolation.ShouldBe(BlobTenantIsolation.Prefix);
    }

    [Fact]
    public void InheritsFromBlobStorageOptions()
    {
        AzureBlobOptions options = new();

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(15));
        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(5));
    }
}

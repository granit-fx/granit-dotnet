using Granit.BlobStorage.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class BlobStorageOptionsTests
{
    [Fact]
    public void SectionName_IsBlobStorage() => BlobStorageOptions.SectionName.ShouldBe("BlobStorage");

    [Fact]
    public void DefaultUploadUrlExpiry_Is15Minutes()
    {
        BlobStorageOptions options = new();

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void DefaultDownloadUrlExpiry_Is5Minutes()
    {
        BlobStorageOptions options = new();

        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void UploadUrlExpiry_CanBeCustomized()
    {
        BlobStorageOptions options = new()
        {
            UploadUrlExpiry = TimeSpan.FromMinutes(30),
        };

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void DownloadUrlExpiry_CanBeCustomized()
    {
        BlobStorageOptions options = new()
        {
            DownloadUrlExpiry = TimeSpan.FromHours(1),
        };

        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromHours(1));
    }
}

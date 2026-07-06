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
    public void DefaultOrphanCleanupAge_Is24Hours()
    {
        BlobStorageOptions options = new();

        options.OrphanCleanupAge.ShouldBe(TimeSpan.FromHours(24));
    }
}

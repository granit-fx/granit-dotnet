using Granit.BlobStorage.DbStore.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.DbStore.Tests;

public sealed class DbStoreBlobOptionsTests
{
    [Fact]
    public void DefaultMaxBlobSizeBytes_Is10MB()
    {
        DbStoreBlobOptions options = new();

        options.MaxBlobSizeBytes.ShouldBe(10 * 1024 * 1024);
    }

    [Fact]
    public void InheritsFromBlobStorageOptions()
    {
        DbStoreBlobOptions options = new();

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(15));
        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void MaxBlobSizeBytes_CanBeCustomized()
    {
        DbStoreBlobOptions options = new()
        {
            MaxBlobSizeBytes = 5 * 1024 * 1024,
        };

        options.MaxBlobSizeBytes.ShouldBe(5 * 1024 * 1024);
    }
}

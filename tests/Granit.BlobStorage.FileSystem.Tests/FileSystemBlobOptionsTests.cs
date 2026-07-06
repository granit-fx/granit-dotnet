using Granit.BlobStorage.FileSystem.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.FileSystem.Tests;

public sealed class FileSystemBlobOptionsTests
{
    [Fact]
    public void DefaultBasePath_IsEmpty()
    {
        FileSystemBlobOptions options = new();

        options.BasePath.ShouldBeEmpty();
    }

    [Fact]
    public void InheritsFromBlobStorageOptions()
    {
        FileSystemBlobOptions options = new();

        options.UploadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(15));
        options.DownloadUrlExpiry.ShouldBe(TimeSpan.FromMinutes(5));
    }
}

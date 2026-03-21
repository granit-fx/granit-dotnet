using Granit.BlobStorage.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Permissions;

public sealed class BlobStoragePermissionsTests
{
    [Fact]
    public void GroupName_IsBlobStorage() => BlobStoragePermissions.GroupName.ShouldBe("BlobStorage");

    [Theory]
    [InlineData("BlobStorage.Blobs.Read")]
    [InlineData("BlobStorage.Blobs.Upload")]
    [InlineData("BlobStorage.Blobs.Download")]
    [InlineData("BlobStorage.Blobs.Delete")]
    [InlineData("BlobStorage.Blobs.Manage")]
    public void BlobPermissions_FollowThreeSegmentConvention(string permissionName)
    {
        string[] segments = permissionName.Split('.');

        segments.Length.ShouldBe(3, $"Permission '{permissionName}' must have 3 dot-separated segments.");
    }

    [Fact]
    public void Blobs_Read_HasCorrectValue() => BlobStoragePermissions.Blobs.Read.ShouldBe("BlobStorage.Blobs.Read");

    [Fact]
    public void Blobs_Upload_HasCorrectValue() => BlobStoragePermissions.Blobs.Upload.ShouldBe("BlobStorage.Blobs.Upload");

    [Fact]
    public void Blobs_Download_HasCorrectValue() => BlobStoragePermissions.Blobs.Download.ShouldBe("BlobStorage.Blobs.Download");

    [Fact]
    public void Blobs_Delete_HasCorrectValue() => BlobStoragePermissions.Blobs.Delete.ShouldBe("BlobStorage.Blobs.Delete");

    [Fact]
    public void Blobs_Manage_HasCorrectValue() => BlobStoragePermissions.Blobs.Manage.ShouldBe("BlobStorage.Blobs.Manage");
}

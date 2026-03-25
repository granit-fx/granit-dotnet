using Granit.BlobStorage.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Permissions;

public sealed class BlobStoragePermissionsTests
{
    [Fact]
    public void GroupName_IsBlobStorage() => BlobStoragePermissions.GroupName.ShouldBe("BlobStorage");

    [Theory]
    [InlineData("BlobStorage.Administration.Read")]
    [InlineData("BlobStorage.Administration.Manage")]
    public void AdministrationPermissions_FollowThreeSegmentConvention(string permissionName)
    {
        string[] segments = permissionName.Split('.');

        segments.Length.ShouldBe(3, $"Permission '{permissionName}' must have 3 dot-separated segments.");
    }

    [Fact]
    public void Administration_Read_HasCorrectValue() => BlobStoragePermissions.Administration.Read.ShouldBe("BlobStorage.Administration.Read");

    [Fact]
    public void Administration_Manage_HasCorrectValue() => BlobStoragePermissions.Administration.Manage.ShouldBe("BlobStorage.Administration.Manage");
}

using Granit.BlobStorage.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Options;

public sealed class BlobStorageEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsExpected() => BlobStorageEndpointsOptions.SectionName.ShouldBe("BlobStorageEndpoints");

    [Fact]
    public void DefaultRoutePrefix_IsBlobs()
    {
        BlobStorageEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("blobs");
    }

    [Fact]
    public void DefaultTagName_IsBlobStorage()
    {
        BlobStorageEndpointsOptions options = new();

        options.TagName.ShouldBe("BlobStorage");
    }

    [Fact]
    public void Properties_CanBeCustomized()
    {
        BlobStorageEndpointsOptions options = new()
        {
            RoutePrefix = "custom-blobs",
            TagName = "Files",
        };

        options.RoutePrefix.ShouldBe("custom-blobs");
        options.TagName.ShouldBe("Files");
    }
}

using Granit.BlobStorage.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Options;

public sealed class BlobStorageEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsExpected() => BlobStorageEndpointsOptions.SectionName.ShouldBe("BlobStorage:Endpoints");

    [Fact]
    public void DefaultRoutePrefix_IsBlobs()
    {
        BlobStorageEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("blob-storage");
    }

    [Fact]
    public void DefaultTagName_IsBlobStorage()
    {
        BlobStorageEndpointsOptions options = new();

        options.TagName.ShouldBe("Blob Storage");
    }
}

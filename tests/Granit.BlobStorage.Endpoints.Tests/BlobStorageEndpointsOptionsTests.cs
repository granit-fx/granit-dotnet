using Granit.BlobStorage.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests;

public sealed class BlobStorageEndpointsOptionsTests
{
    [Fact]
    public void Defaults_should_be_sensible()
    {
        BlobStorageEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("blobs");
        options.RequiredRole.ShouldBe("granit-blobs-admin");
        options.TagName.ShouldBe("BlobStorage");
    }

    [Fact]
    public void SectionName_should_be_BlobStorageEndpoints() =>
        BlobStorageEndpointsOptions.SectionName.ShouldBe("BlobStorageEndpoints");
}

using Granit.BlobStorage.Proxy.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Proxy.Tests;

public sealed class ProxyBlobOptionsTests
{
    [Fact]
    public void SectionName_IsExpected() => ProxyBlobOptions.SectionName.ShouldBe("BlobStorage:Proxy");

    [Fact]
    public void DefaultBaseUrl_IsEmpty()
    {
        ProxyBlobOptions options = new();

        options.BaseUrl.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultRoutePrefix_IsApiBlobs()
    {
        ProxyBlobOptions options = new();

        options.RoutePrefix.ShouldBe("/api/blobs");
    }

    [Fact]
    public void DefaultMaxUploadBytes_Is100MB()
    {
        ProxyBlobOptions options = new();

        options.MaxUploadBytes.ShouldBe(104_857_600L);
    }
}

using Granit.QueryEngine.AspNetCore.Options;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Options;

public sealed class QueryEndpointOptionsTests
{
    [Fact]
    public void TagName_defaults_to_null()
    {
        QueryEndpointOptions options = new();

        options.TagName.ShouldBeNull();
    }

    [Fact]
    public void AuthorizationPolicy_defaults_to_null()
    {
        QueryEndpointOptions options = new();

        options.AuthorizationPolicy.ShouldBeNull();
    }

    [Fact]
    public void IncludeMetaEndpoint_defaults_to_true()
    {
        QueryEndpointOptions options = new();

        options.IncludeMetaEndpoint.ShouldBeTrue();
    }

    [Fact]
    public void TagName_can_be_set()
    {
        QueryEndpointOptions options = new() { TagName = "Products" };

        options.TagName.ShouldBe("Products");
    }

    [Fact]
    public void AuthorizationPolicy_can_be_set()
    {
        QueryEndpointOptions options = new() { AuthorizationPolicy = "AdminOnly" };

        options.AuthorizationPolicy.ShouldBe("AdminOnly");
    }

    [Fact]
    public void IncludeMetaEndpoint_can_be_disabled()
    {
        QueryEndpointOptions options = new() { IncludeMetaEndpoint = false };

        options.IncludeMetaEndpoint.ShouldBeFalse();
    }
}

using Granit.Templating.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests.Options;

public sealed class TemplatingEndpointsOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        TemplatingEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("templating");
        options.TagName.ShouldBe("Templates");
    }

    [Fact]
    public void RoutePrefix_CanBeSet()
    {
        TemplatingEndpointsOptions options = new()
        {
            RoutePrefix = "custom-templates",
        };

        options.RoutePrefix.ShouldBe("custom-templates");
    }

    [Fact]
    public void TagName_CanBeSet()
    {
        TemplatingEndpointsOptions options = new()
        {
            TagName = "CustomTag",
        };

        options.TagName.ShouldBe("CustomTag");
    }
}

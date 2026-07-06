using Granit.Templating.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests.Options;

public sealed class TemplatingEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        TemplatingEndpointsOptions.SectionName.ShouldBe("Templating:Endpoints");

    [Fact]
    public void Defaults_AreCorrect()
    {
        TemplatingEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("templating");
        options.TagName.ShouldBe("Templates");
    }

}

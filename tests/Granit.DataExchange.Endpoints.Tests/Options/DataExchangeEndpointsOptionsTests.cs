using Granit.DataExchange.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Options;

public sealed class DataExchangeEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsDataExchangeEndpoints() =>
        DataExchangeEndpointsOptions.SectionName.ShouldBe("DataExchange:Endpoints");

    [Fact]
    public void Default_RoutePrefix_IsDataExchange()
    {
        DataExchangeEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("data-exchange");
    }

    [Fact]
    public void Default_TagName_IsDataExchange()
    {
        DataExchangeEndpointsOptions options = new();

        options.TagName.ShouldBe("Data Exchange");
    }

    [Fact]
    public void Properties_AreSettable()
    {
        DataExchangeEndpointsOptions options = new()
        {
            RoutePrefix = "custom-prefix",
            TagName = "Custom Tag",
        };

        options.RoutePrefix.ShouldBe("custom-prefix");
        options.TagName.ShouldBe("Custom Tag");
    }
}

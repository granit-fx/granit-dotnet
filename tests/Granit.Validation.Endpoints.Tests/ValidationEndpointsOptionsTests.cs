using Granit.Validation.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_DefaultValue_IsValidation()
    {
        ValidationEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("validation");
    }

    [Fact]
    public void TagName_DefaultValue_IsValidation()
    {
        ValidationEndpointsOptions options = new();

        options.TagName.ShouldBe("Validation");
    }

    [Fact]
    public void RoutePrefix_CanBeChanged()
    {
        ValidationEndpointsOptions options = new()
        {
            RoutePrefix = "custom-prefix",
        };

        options.RoutePrefix.ShouldBe("custom-prefix");
    }

    [Fact]
    public void TagName_CanBeChanged()
    {
        ValidationEndpointsOptions options = new()
        {
            TagName = "CustomTag",
        };

        options.TagName.ShouldBe("CustomTag");
    }
}

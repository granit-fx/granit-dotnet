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

}

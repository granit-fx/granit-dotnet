using Granit.Validation.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests.Options;

public sealed class ValidationEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        ValidationEndpointsOptions.SectionName.ShouldBe("Validation:Endpoints");
}

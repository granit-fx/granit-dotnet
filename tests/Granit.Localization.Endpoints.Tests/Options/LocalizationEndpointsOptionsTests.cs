using Granit.Localization.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests.Options;

public sealed class LocalizationEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        LocalizationEndpointsOptions.SectionName.ShouldBe("Localization:Endpoints");
}

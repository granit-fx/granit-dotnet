using Granit.Settings.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests.Options;

public sealed class SettingsEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        SettingsEndpointsOptions.SectionName.ShouldBe("Settings:Endpoints");
}

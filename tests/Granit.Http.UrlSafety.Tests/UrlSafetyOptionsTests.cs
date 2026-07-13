using Granit.Http.UrlSafety.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.UrlSafety.Tests;

public sealed class UrlSafetyOptionsTests
{
    [Fact]
    public void SectionName_IsHttpUrlSafety() =>
        UrlSafetyOptions.SectionName.ShouldBe("Http:UrlSafety");

    [Fact]
    public void AllowedSchemes_DefaultToHttpsOnly()
    {
        UrlSafetyOptions options = new();

        options.AllowedSchemes.ShouldBe(["https"]);
        options.AllowPrivateNetworks.ShouldBeFalse();
        options.AllowLoopback.ShouldBeFalse();
        options.AllowFileScheme.ShouldBeFalse();
    }
}

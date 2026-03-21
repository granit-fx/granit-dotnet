using Granit.Http.Cookies.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class GranitCookiesOptionsTests
{
    [Fact]
    public void SectionName_IsCookies() =>
        GranitCookiesOptions.SectionName.ShouldBe("Cookies");

    [Fact]
    public void ThrowOnUnregistered_DefaultsToTrue() =>
        new GranitCookiesOptions().ThrowOnUnregistered.ShouldBeTrue();

    [Fact]
    public void DefaultRetentionDays_DefaultsTo365() =>
        new GranitCookiesOptions().DefaultRetentionDays.ShouldBe(365);

    [Fact]
    public void ThirdPartyServices_DefaultsToEmpty() =>
        new GranitCookiesOptions().ThirdPartyServices.ShouldBeEmpty();
}

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

    // VULN-208 — default dropped from 365 to 30 days to align with RGPD
    // minimisation (Art. 5(1)(e)). Cookies that legitimately need a longer
    // lifetime must declare an explicit CookieDefinition.RetentionDays.
    [Fact]
    public void DefaultRetentionDays_DefaultsTo30() =>
        new GranitCookiesOptions().DefaultRetentionDays.ShouldBe(30);

    [Fact]
    public void MaxRetentionDays_Is395DaysForCnilCap() =>
        GranitCookiesOptions.MaxRetentionDays.ShouldBe(395);

    [Fact]
    public void ThirdPartyServices_DefaultsToEmpty() =>
        new GranitCookiesOptions().ThirdPartyServices.ShouldBeEmpty();
}

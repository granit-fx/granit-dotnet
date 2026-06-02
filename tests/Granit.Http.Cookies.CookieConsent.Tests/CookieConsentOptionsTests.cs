using Granit.Http.Cookies.CookieConsent.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.CookieConsent.Tests;

public sealed class CookieConsentOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        CookieConsentOptions.SectionName.ShouldBe("Http:Cookies:CookieConsent");

    [Fact]
    public void DefaultCookieName_IsCcCookie()
    {
        CookieConsentOptions options = new();

        options.CookieName.ShouldBe("cc_cookie");
    }

    [Fact]
    public void DefaultFunctionalCategoryName_IsFunctional()
    {
        CookieConsentOptions options = new();

        options.FunctionalCategoryName.ShouldBe("functional");
    }

    [Fact]
    public void DefaultAnalyticsCategoryName_IsAnalytics()
    {
        CookieConsentOptions options = new();

        options.AnalyticsCategoryName.ShouldBe("analytics");
    }

    [Fact]
    public void DefaultMarketingCategoryName_IsMarketing()
    {
        CookieConsentOptions options = new();

        options.MarketingCategoryName.ShouldBe("marketing");
    }

    [Fact]
    public void DefaultSaleOrSharingCategoryName_IsSaleOrSharing()
    {
        CookieConsentOptions options = new();

        options.SaleOrSharingCategoryName.ShouldBe("sale_or_sharing");
    }
}

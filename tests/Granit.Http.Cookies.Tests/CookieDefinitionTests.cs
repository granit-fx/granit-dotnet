using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookieDefinitionTests
{
    [Fact]
    public void InitProperties_HaveSensibleDefaults()
    {
        CookieDefinition definition = new("test", CookieCategory.Analytics, 365, false, "Test");

        definition.SameSite.ShouldBe(SameSiteMode.Lax);
        definition.Path.ShouldBe("/");
        definition.IsEssential.ShouldBeFalse();
    }

    [Theory]
    [InlineData(CookieCategory.StrictlyNecessary, true)]
    [InlineData(CookieCategory.Preferences, false)]
    [InlineData(CookieCategory.Analytics, false)]
    [InlineData(CookieCategory.Marketing, false)]
    [InlineData(CookieCategory.SaleOrSharing, false)]
    public void IsEssential_IsDerivedFromStrictlyNecessaryCategory(CookieCategory category, bool expected)
    {
        CookieDefinition definition = new("cookie", category, 1, true, "Test");

        definition.IsEssential.ShouldBe(expected);
    }
}

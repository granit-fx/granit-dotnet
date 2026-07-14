using Shouldly;
using Xunit;
namespace Granit.Http.Cookies.Tests;

/// <summary>Snake_case wire-name vocabulary tests for <see cref="CookieCategoryNames"/>.</summary>
public sealed class CookieCategoryNamesTests
{
    [Theory]
    [InlineData(CookieCategory.StrictlyNecessary, "strictly_necessary")]
    [InlineData(CookieCategory.Preferences, "preferences")]
    [InlineData(CookieCategory.Analytics, "analytics")]
    [InlineData(CookieCategory.Marketing, "marketing")]
    [InlineData(CookieCategory.SaleOrSharing, "sale_or_sharing")]
    public void ToSnakeCase_MapsEveryCategory_Explicitly(CookieCategory category, string expected) =>
        CookieCategoryNames.ToSnakeCase(category).ShouldBe(expected);

    [Fact]
    public void All_ContainsOneNamePerEnumValue() =>
        CookieCategoryNames.All.Count.ShouldBe(Enum.GetValues<CookieCategory>().Length);

    [Theory]
    [InlineData("analytics", true)]
    [InlineData("sale_or_sharing", true)]
    [InlineData("Analytics", false)] // PascalCase is not the wire format
    [InlineData("tracking", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKnown_AcceptsOnlyTheClosedVocabulary(string? name, bool expected) =>
        CookieCategoryNames.IsKnown(name).ShouldBe(expected);
}

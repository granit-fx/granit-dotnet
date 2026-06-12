using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookieDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        CookieDefinition definition = new("session", CookieCategory.StrictlyNecessary, 1, true, "Session management");

        definition.Name.ShouldBe("session");
        definition.Category.ShouldBe(CookieCategory.StrictlyNecessary);
        definition.RetentionDays.ShouldBe(1);
        definition.IsHttpOnly.ShouldBeTrue();
        definition.Purpose.ShouldBe("Session management");
    }

    [Fact]
    public void InitProperties_HaveSensibleDefaults()
    {
        CookieDefinition definition = new("test", CookieCategory.Analytics, 365, false, "Test");

        definition.SameSite.ShouldBe(SameSiteMode.Lax);
        definition.Path.ShouldBe("/");
        definition.IsEssential.ShouldBeFalse();
    }

    [Fact]
    public void InitProperties_CanBeOverridden()
    {
        CookieDefinition definition = new("__Host-bff", CookieCategory.StrictlyNecessary, 1, true, "BFF")
        {
            SameSite = SameSiteMode.Strict,
            Path = "/",
        };

        definition.SameSite.ShouldBe(SameSiteMode.Strict);
        definition.Path.ShouldBe("/");
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

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        CookieDefinition a = new("session", CookieCategory.StrictlyNecessary, 1, true, "Session");
        CookieDefinition b = new("session", CookieCategory.StrictlyNecessary, 1, true, "Session");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        CookieDefinition a = new("session", CookieCategory.StrictlyNecessary, 1, true, "Session");
        CookieDefinition b = new("analytics", CookieCategory.Analytics, 365, false, "Analytics");

        a.ShouldNotBe(b);
    }
}

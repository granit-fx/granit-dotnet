using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class ThirdPartyServiceDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        List<string> patterns = ["^_pk_", "^mtm_"];
        ThirdPartyServiceDefinition definition = new("matomo", CookieCategory.Analytics, patterns);

        definition.Name.ShouldBe("matomo");
        definition.Category.ShouldBe(CookieCategory.Analytics);
        definition.CookiePatterns.ShouldBe(patterns);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        List<string> patterns = ["^_pk_"];
        ThirdPartyServiceDefinition a = new("matomo", CookieCategory.Analytics, patterns);
        ThirdPartyServiceDefinition b = new("matomo", CookieCategory.Analytics, patterns);

        a.ShouldBe(b);
    }
}

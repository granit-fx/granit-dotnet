using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookieCategoryTests
{
    [Fact]
    public void StrictlyNecessary_HasValue0() =>
        ((int)CookieCategory.StrictlyNecessary).ShouldBe(0);

    [Fact]
    public void Preferences_HasValue1() =>
        ((int)CookieCategory.Preferences).ShouldBe(1);

    [Fact]
    public void Analytics_HasValue2() =>
        ((int)CookieCategory.Analytics).ShouldBe(2);

    [Fact]
    public void Marketing_HasValue3() =>
        ((int)CookieCategory.Marketing).ShouldBe(3);

    [Fact]
    public void Enum_HasFourValues() =>
        Enum.GetValues<CookieCategory>().Length.ShouldBe(4);
}

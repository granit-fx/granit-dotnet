using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class CookieCategoryTests
{
    [Fact]
    public void Enum_HasFiveValues() =>
        Enum.GetValues<CookieCategory>().Length.ShouldBe(5);
}

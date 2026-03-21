using Granit.Http.Cookies.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Tests;

public sealed class UnregisteredCookieExceptionTests
{
    [Fact]
    public void CookieName_ReturnsProvidedName()
    {
        UnregisteredCookieException exception = new("my_cookie");

        exception.CookieName.ShouldBe("my_cookie");
    }

    [Fact]
    public void Message_ContainsCookieName()
    {
        UnregisteredCookieException exception = new("analytics_id");

        exception.Message.ShouldContain("analytics_id");
    }

    [Fact]
    public void Message_DescribesRegistrationRequirement()
    {
        UnregisteredCookieException exception = new("test");

        exception.Message.ShouldContain("not registered");
    }
}

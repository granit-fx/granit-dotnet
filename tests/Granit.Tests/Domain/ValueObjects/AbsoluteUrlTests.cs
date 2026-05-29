using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class AbsoluteUrlTests
{
    [Theory]
    [InlineData("https://www.acme.com")]
    [InlineData("https://acme.com/about/team?x=1#frag")]
    [InlineData("http://localhost:3000/preview")] // dev / preview origins are http
    [InlineData("http://127.0.0.1:5000")]
    public void Create_AbsoluteHttpOrHttps_Succeeds(string value)
    {
        AbsoluteUrl.Create(value).Value.ShouldBe(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/relative/path")]
    [InlineData("www.acme.com")]
    [InlineData("ftp://acme.com/file")]
    [InlineData("mailto:hi@acme.com")]
    [InlineData("javascript:alert(1)")]
    public void Create_NotAbsoluteHttpUrl_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => AbsoluteUrl.Create(value));
    }

    [Fact]
    public void Create_TooLong_Throws()
    {
        string tooLong = "https://acme.com/" + new string('a', 2048);

        Should.Throw<ArgumentException>(() => AbsoluteUrl.Create(tooLong));
    }

    [Fact]
    public void ImplicitConversions_RoundTrip()
    {
        AbsoluteUrl url = "https://acme.com";
        string raw = url;

        raw.ShouldBe("https://acme.com");
    }

    [Fact]
    public void Equality_IsStructural()
    {
        AbsoluteUrl.Create("https://acme.com").ShouldBe(AbsoluteUrl.Create("https://acme.com"));
        AbsoluteUrl.Create("https://acme.com").ShouldNotBe(AbsoluteUrl.Create("https://acme.org"));
    }

    [Fact]
    public void IsSingleValueObject()
    {
        AbsoluteUrl.Create("https://acme.com").ShouldBeAssignableTo<ValueObject>();
    }
}

using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class HttpsUrlTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://example.com/webhooks")]
    [InlineData("https://sub.domain.example.com:8443/path?key=value")]
    public void Create_ValidHttpsUrl_Succeeds(string url)
    {
        var result = HttpsUrl.Create(url);

        result.Value.ShouldBe(url);
    }

    [Fact]
    public void Create_NullOrWhitespace_Throws()
    {
        Should.Throw<ArgumentException>(() => HttpsUrl.Create(null!));
        Should.Throw<ArgumentException>(() => HttpsUrl.Create(""));
        Should.Throw<ArgumentException>(() => HttpsUrl.Create("   "));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("not-a-url")]
    [InlineData("//example.com")]
    public void Create_NonHttpsUrl_Throws(string url)
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() => HttpsUrl.Create(url));

        ex.Message.ShouldContain("HTTPS");
    }

    [Fact]
    public void Create_ExceedsMaxLength_Throws()
    {
        string longUrl = "https://example.com/" + new string('a', 2048);

        ArgumentException ex = Should.Throw<ArgumentException>(() => HttpsUrl.Create(longUrl));

        ex.Message.ShouldContain("2048");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        var url = HttpsUrl.Create("https://example.com/hook");

        string result = url;

        result.ShouldBe("https://example.com/hook");
    }

    [Fact]
    public void ImplicitConversion_FromString_CreatesInstance()
    {
        HttpsUrl url = "https://example.com/hook";

        url.Value.ShouldBe("https://example.com/hook");
    }

    [Fact]
    public void Equality_SameUrl_AreEqual()
    {
        var a = HttpsUrl.Create("https://example.com");
        var b = HttpsUrl.Create("https://example.com");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Equality_DifferentUrl_AreNotEqual()
    {
        var a = HttpsUrl.Create("https://example.com/a");
        var b = HttpsUrl.Create("https://example.com/b");

        a.ShouldNotBe(b);
        (a != b).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsUrlString()
    {
        var url = HttpsUrl.Create("https://example.com");

        url.ToString().ShouldBe("https://example.com");
    }
}

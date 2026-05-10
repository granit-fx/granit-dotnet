using System;
using Granit.Browsing.Pages;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Pages;

public sealed class RoutePatternTests
{
    [Theory]
    [InlineData("**/*", "https://api.example.com/foo", true)]
    [InlineData("**.example.com/**", "https://api.example.com/foo", true)]
    [InlineData("**.example.com/**", "https://example.com/foo", false)] // glob requires a subdomain
    [InlineData("example.com/**", "https://example.com/foo", true)]
    [InlineData("api.example.com/**", "https://API.example.com/foo", true)]
    [InlineData("api.example.com/v1/**", "https://api.example.com/v2/users", false)]
    [InlineData("api.example.com/v1/**", "https://api.example.com/v1/users/42", true)]
    public void IsMatch_should_respect_glob(string glob, string url, bool expected) =>
        RoutePattern.Parse(glob).IsMatch(new Uri(url)).ShouldBe(expected);

    [Fact]
    public void Parse_should_reject_empty_glob() =>
        Should.Throw<ArgumentException>(() => RoutePattern.Parse(""));

    [Fact]
    public void Parse_should_reject_whitespace_glob() =>
        Should.Throw<ArgumentException>(() => RoutePattern.Parse("   "));

    [Fact]
    public void Parse_should_reject_control_characters() =>
        Should.Throw<ArgumentException>(() => RoutePattern.Parse("api.example.com/\nfoo"));

    [Fact]
    public void Equality_should_be_value_based()
    {
        var a = RoutePattern.Parse("**/*");
        var b = RoutePattern.Parse("**/*");

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void ToString_should_return_glob()
    {
        var p = RoutePattern.Parse("api.example.com/**");

        p.ToString().ShouldBe("api.example.com/**");
    }
}

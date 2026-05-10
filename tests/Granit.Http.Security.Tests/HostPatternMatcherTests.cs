using Granit.Http.Security.Internal;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class HostPatternMatcherTests
{
    [Theory]
    // Star alone matches everything.
    [InlineData("foo.bar.com", "*")]
    // Exact match (case-insensitive).
    [InlineData("api.example.com", "api.example.com")]
    [InlineData("API.EXAMPLE.COM", "api.example.com")]
    // *. matches subdomains and apex.
    [InlineData("api.example.com", "*.example.com")]
    [InlineData("example.com", "*.example.com")]
    [InlineData("deep.nested.api.example.com", "*.example.com")]
    public void Matches_Hits(string host, string pattern) =>
        HostPatternMatcher.Matches(host, pattern).ShouldBeTrue();

    [Theory]
    [InlineData("api.example.com", "other.com")]
    [InlineData("evilexample.com", "*.example.com")]
    [InlineData("example.com.evil.com", "*.example.com")]
    public void Matches_Misses(string host, string pattern) =>
        HostPatternMatcher.Matches(host, pattern).ShouldBeFalse();

    [Fact]
    public void Matches_IdnPatternAndHost()
    {
        // Unicode pattern + ASCII host (punycode).
        HostPatternMatcher.Matches("xn--e1afmkfd.xn--p1ai", "*.xn--p1ai")
            .ShouldBeTrue();
        // Unicode pattern (will be punycoded internally).
        HostPatternMatcher.Matches("xn--e1afmkfd.xn--p1ai", "*.рф")
            .ShouldBeTrue();
    }

    [Fact]
    public void MatchesAny_FirstMatchWins()
    {
        string[] patterns = ["*.example.com", "github.com"];

        HostPatternMatcher.MatchesAny("github.com", patterns).ShouldBeTrue();
        HostPatternMatcher.MatchesAny("api.example.com", patterns).ShouldBeTrue();
        HostPatternMatcher.MatchesAny("nope.org", patterns).ShouldBeFalse();
    }

    [Fact]
    public void Matches_RejectsEmpty()
    {
        Should.Throw<ArgumentException>(() => HostPatternMatcher.Matches("", "a"));
        Should.Throw<ArgumentException>(() => HostPatternMatcher.Matches("a", ""));
    }
}

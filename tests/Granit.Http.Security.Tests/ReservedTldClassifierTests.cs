using Granit.Http.Security;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class ReservedTldClassifierTests
{
    [Theory]
    [InlineData("printer.local", "local")]
    [InlineData("svc.internal", "internal")]
    [InlineData("localhost", "localhost")]
    [InlineData("api.localhost", "localhost")]
    [InlineData("3g2upl4pq6kufc4m.onion", "onion")]
    [InlineData("foo.test", "test")]
    [InlineData("example.com.example", "example")]
    [InlineData("missing.invalid", "invalid")]
    // Case-insensitive matching.
    [InlineData("PRINTER.LOCAL", "local")]
    [InlineData("Some.Internal", "internal")]
    public void IsReserved_Hits(string host, string expectedTld)
    {
        bool reserved = ReservedTldClassifier.IsReserved(host, out string tld);

        reserved.ShouldBeTrue();
        tld.ShouldBe(expectedTld);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("api.example.org")]
    [InlineData("granit.dev")]
    [InlineData("not-local.io")]
    public void IsReserved_Misses(string host)
    {
        bool reserved = ReservedTldClassifier.IsReserved(host, out string tld);

        reserved.ShouldBeFalse();
        tld.ShouldBeEmpty();
    }

    [Fact]
    public void IsReserved_EmptyThrows() =>
        Should.Throw<ArgumentException>(() => ReservedTldClassifier.IsReserved("", out _));
}

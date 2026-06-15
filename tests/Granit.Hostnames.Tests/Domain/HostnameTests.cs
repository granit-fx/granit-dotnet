using Granit.Hostnames.Domain;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Tests.Domain;

public sealed class HostnameTests
{
    [Theory]
    [InlineData("Www.Acme.COM", "www.acme.com")]
    [InlineData("  example.com  ", "example.com")]
    [InlineData("sub.domain.example.co.uk", "sub.domain.example.co.uk")]
    public void Create_normalises_to_lowercase_and_trims(string input, string expected)
    {
        var host = Hostname.Create(input);

        host.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nodot")]
    [InlineData("bad_underscore.com")]
    [InlineData("-leading.com")]
    [InlineData("trailing-.com")]
    [InlineData("spaces in.com")]
    public void Create_rejects_invalid_hostnames(string input) =>
        Should.Throw<ArgumentException>(() => Hostname.Create(input));

    [Theory]
    [InlineData("169.254.169.254")] // AWS metadata endpoint
    [InlineData("10.0.0.1")]        // RFC-1918 private
    [InlineData("192.168.1.100")]   // RFC-1918 private
    [InlineData("127.0.0.1")]       // loopback
    [InlineData("8.8.8.8")]         // any all-numeric labels
    public void Create_rejects_ip_address_literals(string input) =>
        // RFC 1123 §2.1: the TLD must not be all-numeric.
        Should.Throw<ArgumentException>(() => Hostname.Create(input));

    [Fact]
    public void Create_rejects_hostnames_over_253_characters()
    {
        string tooLong = string.Join(".", Enumerable.Repeat("aaaaaaaaaa", 30)) + ".com"; // > 253

        Should.Throw<ArgumentException>(() => Hostname.Create(tooLong));
    }

    [Fact]
    public void Implicit_operators_round_trip_through_string()
    {
        Hostname host = "acme.com";
        string asString = host;

        asString.ShouldBe("acme.com");
    }

    [Fact]
    public void Equality_is_structural_over_the_value()
    {
        Hostname.Create("acme.com").ShouldBe(Hostname.Create("ACME.com"));
        Hostname.Create("acme.com").ShouldNotBe(Hostname.Create("other.com"));
    }
}

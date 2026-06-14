using System.Net;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class IpAddressAnonymizerTests
{
    [Theory]
    [InlineData("192.168.1.42", "192.168.1.0")]
    [InlineData("8.8.8.8", "8.8.8.0")]
    [InlineData("255.255.255.255", "255.255.255.0")]
    [InlineData("10.0.0.1", "10.0.0.0")]
    public void Mask_IPv4_ZeroesLastOctet(string input, string expected) =>
        IpAddressAnonymizer.Mask(input).ShouldBe(expected);

    [Theory]
    // /48 keeps the first 3 hextets, zeroes the rest.
    [InlineData("2001:db8:85a3:1:2:3:4:5", "2001:db8:85a3::")]
    [InlineData("fe80::1", "fe80::")]
    public void Mask_IPv6_MasksTo48(string input, string expected) =>
        IpAddressAnonymizer.Mask(input).ShouldBe(expected);

    [Fact]
    public void Mask_IPv4MappedIPv6_UnwrapsThenMasksAsIPv4() =>
        IpAddressAnonymizer.Mask("::ffff:192.168.1.42").ShouldBe("192.168.1.0");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("999.1.1.1")]
    public void Mask_NullOrUnparseable_ReturnsNull(string? input) =>
        IpAddressAnonymizer.Mask(input).ShouldBeNull();

    [Fact]
    public void Mask_NullIpAddress_ReturnsNull() =>
        IpAddressAnonymizer.Mask((IPAddress?)null).ShouldBeNull();

    [Fact]
    public void Mask_IsStable_SameInputSameOutput()
    {
        string? first = IpAddressAnonymizer.Mask("203.0.113.55");
        string? second = IpAddressAnonymizer.Mask("203.0.113.99");
        // Both addresses in the same /24 collapse to the same masked value.
        first.ShouldBe("203.0.113.0");
        second.ShouldBe(first);
    }
}

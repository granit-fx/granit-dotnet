using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.Tests;

public sealed class IpMaskingTests
{
    [Theory]
    [InlineData("203.0.113.42", "203.0.113.0")]
    [InlineData("8.8.8.8", "8.8.8.0")]
    [InlineData("1.2.3.255", "1.2.3.0")]
    public void Mask_IPv4_ZeroesLastOctet(string input, string expected) =>
        IpMasking.Mask(input).ShouldBe(expected);

    [Theory]
    [InlineData("2001:db8:1234:5678::1", "2001:db8:1234::")]
    [InlineData("2a00:1450:4001:81b::200e", "2a00:1450:4001::")]
    [InlineData("fe80::1ff:fe23:4567:890a", "fe80::")]
    public void Mask_IPv6_KeepsFirst48Bits(string input, string expected) =>
        IpMasking.Mask(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    public void Mask_InvalidInput_ReturnsNull(string? input) =>
        IpMasking.Mask(input).ShouldBeNull();
}

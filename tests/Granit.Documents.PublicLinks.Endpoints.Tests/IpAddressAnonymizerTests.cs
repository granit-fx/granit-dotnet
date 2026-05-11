using System.Net;
using Granit.Documents.PublicLinks.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Documents.PublicLinks.Endpoints.Tests;

/// <summary>
/// Unit tests for <see cref="IpAddressAnonymizer"/> (F18.4). Verifies the /24 (IPv4)
/// and /48 (IPv6) masks documented for the public-link audit trail.
/// </summary>
public sealed class IpAddressAnonymizerTests
{
    [Fact]
    public void Mask_IPv4_ZeroesLastOctet()
    {
        IpAddressAnonymizer.Mask(IPAddress.Parse("203.0.113.42")).ShouldBe("203.0.113.0");
    }

    [Fact]
    public void Mask_IPv6_ZeroesLastTenBytes()
    {
        // /48 keeps the first three hextets; remaining 80 bits zeroed.
        IpAddressAnonymizer.Mask(IPAddress.Parse("2001:db8:cafe:1::dead:beef")).ShouldBe("2001:db8:cafe::");
    }

    [Fact]
    public void Mask_IPv4MappedIPv6_UnwrapsBeforeMasking()
    {
        IpAddressAnonymizer.Mask(IPAddress.Parse("::ffff:198.51.100.7")).ShouldBe("198.51.100.0");
    }

    [Fact]
    public void Mask_Null_ReturnsNull()
    {
        IpAddressAnonymizer.Mask((IPAddress?)null).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    public void Mask_InvalidString_ReturnsNull(string? raw)
    {
        IpAddressAnonymizer.Mask(raw).ShouldBeNull();
    }

    [Fact]
    public void Mask_ValidIPv4String_ReturnsMaskedString()
    {
        IpAddressAnonymizer.Mask("10.20.30.40").ShouldBe("10.20.30.0");
    }
}

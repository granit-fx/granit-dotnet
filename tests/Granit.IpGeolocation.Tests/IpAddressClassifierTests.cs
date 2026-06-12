using System.Net;
using Granit.IpGeolocation.Internal;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.Tests;

public sealed class IpAddressClassifierTests
{
    [Theory]
    [InlineData("10.0.0.1")]        // RFC 1918
    [InlineData("172.16.5.4")]      // RFC 1918
    [InlineData("192.168.1.1")]     // RFC 1918
    [InlineData("127.0.0.1")]       // loopback
    [InlineData("169.254.1.1")]     // link-local
    [InlineData("100.64.0.1")]      // CGNAT
    [InlineData("0.0.0.0")]         // unspecified
    [InlineData("224.0.0.1")]       // multicast 224.0.0.0/4
    [InlineData("239.255.255.250")] // multicast (SSDP)
    [InlineData("240.0.0.1")]       // reserved 240.0.0.0/4
    [InlineData("255.255.255.255")] // broadcast
    [InlineData("::1")]             // IPv6 loopback
    [InlineData("fe80::1")]         // IPv6 link-local
    [InlineData("fc00::1")]         // IPv6 unique-local
    public void IsPrivateOrReserved_NonPublicAddresses_ReturnsTrue(string ip) =>
        IpAddressClassifier.IsPrivateOrReserved(IPAddress.Parse(ip)).ShouldBeTrue();

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("203.0.113.5")]
    [InlineData("2a00:1450:4001:81b::200e")]
    public void IsPrivateOrReserved_PublicAddresses_ReturnsFalse(string ip) =>
        IpAddressClassifier.IsPrivateOrReserved(IPAddress.Parse(ip)).ShouldBeFalse();
}

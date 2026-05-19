using System.Net;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class PrivateNetworkClassifierTests
{
    [Theory]
    // 0.0.0.0/8 — "this network"
    [InlineData("0.0.0.0", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("0.255.255.255", UrlSafetyViolationKind.PrivateNetwork)]
    // 10.0.0.0/8
    [InlineData("10.0.0.0", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("10.255.255.255", UrlSafetyViolationKind.PrivateNetwork)]
    // 100.64.0.0/10
    [InlineData("100.64.0.0", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("100.127.255.255", UrlSafetyViolationKind.PrivateNetwork)]
    // 127.0.0.0/8
    [InlineData("127.0.0.1", UrlSafetyViolationKind.Loopback)]
    [InlineData("127.255.255.255", UrlSafetyViolationKind.Loopback)]
    // 169.254.0.0/16 link-local — except 169.254.169.254
    [InlineData("169.254.0.1", UrlSafetyViolationKind.LinkLocal)]
    [InlineData("169.254.255.255", UrlSafetyViolationKind.LinkLocal)]
    [InlineData("169.254.169.254", UrlSafetyViolationKind.MetadataEndpoint)]
    // 172.16.0.0/12
    [InlineData("172.16.0.0", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("172.31.255.255", UrlSafetyViolationKind.PrivateNetwork)]
    // 192.168.0.0/16
    [InlineData("192.168.0.0", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("192.168.255.255", UrlSafetyViolationKind.PrivateNetwork)]
    // IPv6
    [InlineData("::1", UrlSafetyViolationKind.Loopback)]
    [InlineData("fe80::1", UrlSafetyViolationKind.LinkLocal)]
    [InlineData("febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff", UrlSafetyViolationKind.LinkLocal)]
    [InlineData("fc00::1", UrlSafetyViolationKind.IPv6UniqueLocal)]
    [InlineData("fd12:3456:789a::1", UrlSafetyViolationKind.IPv6UniqueLocal)]
    [InlineData("fd00:ec2::254", UrlSafetyViolationKind.MetadataEndpoint)]
    [InlineData("fe80::a9fe:a9fe", UrlSafetyViolationKind.MetadataEndpoint)]
    public void Classify_Blocked(string ipText, UrlSafetyViolationKind expected)
    {
        var ip = IPAddress.Parse(ipText);

        bool blocked = PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind);

        blocked.ShouldBeTrue();
        kind.ShouldBe(expected);
    }

    [Theory]
    // Boundary "allowed" addresses just outside each blocked range.
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("9.255.255.255")]
    [InlineData("11.0.0.0")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.0")]
    [InlineData("128.0.0.0")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.0")]
    [InlineData("169.253.255.255")]
    [InlineData("169.255.0.0")]
    [InlineData("192.167.255.255")]
    [InlineData("192.169.0.0")]
    // IPv6
    [InlineData("2001:4860:4860::8888")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("fb00::1")] // just outside fc00::/7 lower bound
    [InlineData("fe00::1")] // 0xfe with high bits 00 — not link-local
    [InlineData("fec0::1")] // site-local (deprecated) — not blocked by our policy
    public void Classify_Allowed(string ipText)
    {
        var ip = IPAddress.Parse(ipText);

        bool blocked = PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind);

        blocked.ShouldBeFalse();
        kind.ShouldBe(default);
    }

    [Fact]
    public void Classify_IPv4MappedIPv6_LoopbackUnwraps()
    {
        var ip = IPAddress.Parse("::ffff:127.0.0.1");

        bool blocked = PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind);

        blocked.ShouldBeTrue();
        kind.ShouldBe(UrlSafetyViolationKind.Loopback);
    }

    [Fact]
    public void Classify_IPv4MappedIPv6_Public()
    {
        var ip = IPAddress.Parse("::ffff:8.8.8.8");

        bool blocked = PrivateNetworkClassifier.Classify(ip, out _);

        blocked.ShouldBeFalse();
    }

    [Fact]
    public void Classify_NullArgument_Throws() =>
        Should.Throw<ArgumentNullException>(() =>
            PrivateNetworkClassifier.Classify(null!, out _));

    [Theory]
    // VULN-200 — reserved ranges
    [InlineData("224.0.0.1", UrlSafetyViolationKind.ReservedAddress)]          // multicast
    [InlineData("239.255.255.255", UrlSafetyViolationKind.ReservedAddress)]    // multicast
    [InlineData("240.0.0.0", UrlSafetyViolationKind.ReservedAddress)]          // reserved future
    [InlineData("255.255.255.255", UrlSafetyViolationKind.ReservedAddress)]    // limited broadcast
    [InlineData("192.0.0.1", UrlSafetyViolationKind.PrivateNetwork)]           // IETF assignments
    [InlineData("192.0.2.1", UrlSafetyViolationKind.PrivateNetwork)]           // TEST-NET-1
    [InlineData("198.51.100.1", UrlSafetyViolationKind.PrivateNetwork)]        // TEST-NET-2
    [InlineData("203.0.113.1", UrlSafetyViolationKind.PrivateNetwork)]         // TEST-NET-3
    [InlineData("198.18.0.1", UrlSafetyViolationKind.PrivateNetwork)]          // benchmark
    [InlineData("198.19.255.254", UrlSafetyViolationKind.PrivateNetwork)]      // benchmark
    [InlineData("::", UrlSafetyViolationKind.Loopback)]                        // unspecified
    [InlineData("ff00::1", UrlSafetyViolationKind.ReservedAddress)]            // IPv6 multicast
    [InlineData("ff02::1", UrlSafetyViolationKind.ReservedAddress)]            // all-nodes multicast
    [InlineData("2001:db8::1", UrlSafetyViolationKind.ReservedAddress)]        // documentation
    public void Classify_NewReservedRanges_Blocked(string ipText, UrlSafetyViolationKind expected)
    {
        var ip = IPAddress.Parse(ipText);

        bool blocked = PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind);

        blocked.ShouldBeTrue();
        kind.ShouldBe(expected);
    }

    [Theory]
    // VULN-100 — IPv6 transitional / embedded-IPv4 bypass primitives
    // NAT64 64:ff9b::/96 wrapping loopback / IMDS / RFC1918
    [InlineData("64:ff9b::7f00:1", UrlSafetyViolationKind.IPv6EmbeddedIPv4)]      // → 127.0.0.1
    [InlineData("64:ff9b::a9fe:a9fe", UrlSafetyViolationKind.MetadataEndpoint)]   // → 169.254.169.254
    [InlineData("64:ff9b::a00:1", UrlSafetyViolationKind.IPv6EmbeddedIPv4)]       // → 10.0.0.1
    // 6to4 2002::/16 — bytes 2..5 encode the IPv4
    [InlineData("2002:7f00:1::", UrlSafetyViolationKind.IPv6EmbeddedIPv4)]        // → 127.0.0.1
    [InlineData("2002:a9fe:a9fe::", UrlSafetyViolationKind.MetadataEndpoint)]     // → 169.254.169.254
    // Teredo 2001::/32 — last 4 bytes XOR 0xff = client IPv4
    // 0x80 0x00 0xff 0xfe ^ 0xff = 0x7f 0xff 0x00 0x01 = 127.255.0.1 (loopback /8)
    [InlineData("2001:0:0:0:0:0:8000:fffe", UrlSafetyViolationKind.IPv6EmbeddedIPv4)]
    // IPv4-compatible IPv6 ::a.b.c.d
    [InlineData("::127.0.0.1", UrlSafetyViolationKind.IPv6EmbeddedIPv4)]
    [InlineData("::169.254.169.254", UrlSafetyViolationKind.MetadataEndpoint)]
    public void Classify_IPv6TransitionalEmbeddedIPv4_Blocked(string ipText, UrlSafetyViolationKind expected)
    {
        var ip = IPAddress.Parse(ipText);

        bool blocked = PrivateNetworkClassifier.Classify(ip, out UrlSafetyViolationKind kind);

        blocked.ShouldBeTrue();
        kind.ShouldBe(expected);
    }

    [Theory]
    // 6to4 / NAT64 to a PUBLIC IPv4 — must remain unblocked
    [InlineData("64:ff9b::808:808")]   // NAT64 → 8.8.8.8
    [InlineData("2002:808:808::")]      // 6to4 → 8.8.8.8
    public void Classify_IPv6TransitionalEmbeddedPublicIPv4_Allowed(string ipText)
    {
        var ip = IPAddress.Parse(ipText);

        bool blocked = PrivateNetworkClassifier.Classify(ip, out _);

        blocked.ShouldBeFalse();
    }
}

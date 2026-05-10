using System.Net;
using Granit.Http.Security;
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
}

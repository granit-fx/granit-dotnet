using Granit.Privacy.Endpoints.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Extensions;

public sealed class PseudonymizeIpAddressTests
{
    [Theory]
    [InlineData("192.168.1.42", "192.168.0.0")]
    [InlineData("10.0.0.1", "10.0.0.0")]
    [InlineData("172.16.254.255", "172.16.0.0")]
    public void IPv4_MasksToSlash16(string input, string expected) =>
        PrivacyEndpointRouteBuilderExtensions.PseudonymizeIpAddress(input).ShouldBe(expected);

    [Theory]
    [InlineData("2001:db8::1", "2001:db8::")]
    [InlineData("::1", "::")]
    [InlineData("2001:db8:85a3::8a2e:370:7334", "2001:db8:85a3::")]
    public void IPv6_MasksToSlash48(string input, string expected) =>
        PrivacyEndpointRouteBuilderExtensions.PseudonymizeIpAddress(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmpty_ReturnsNull(string? input) =>
        PrivacyEndpointRouteBuilderExtensions.PseudonymizeIpAddress(input).ShouldBeNull();
}

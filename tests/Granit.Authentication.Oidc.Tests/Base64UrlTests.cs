using Granit.Authentication.Oidc.Internal;
using Shouldly;
using Xunit;

namespace Granit.Authentication.Oidc.Tests;

public sealed class Base64UrlTests
{
    [Fact]
    public void Encode_ProducesUrlSafeOutput()
    {
        // Use bytes that would produce +, /, = in standard base64
        byte[] data = [0xFB, 0xFF, 0xFE, 0x3E, 0x3F];

        string encoded = Base64Url.Encode(data);

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
    }

    [Fact]
    public void Decode_RoundTrips()
    {
        byte[] original = [0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD, 0x80, 0x7F];

        string encoded = Base64Url.Encode(original);
        byte[] decoded = Base64Url.Decode(encoded);

        decoded.ShouldBe(original);
    }

    [Theory]
    [InlineData(new byte[] { 0x01 })]           // 1 byte -> base64 length % 4 == 2
    [InlineData(new byte[] { 0x01, 0x02 })]     // 2 bytes -> base64 length % 4 == 3
    [InlineData(new byte[] { 0x01, 0x02, 0x03 })] // 3 bytes -> base64 length % 4 == 0
    public void Decode_HandlesVariousPaddingLengths(byte[] original)
    {
        string encoded = Base64Url.Encode(original);

        byte[] decoded = Base64Url.Decode(encoded);

        decoded.ShouldBe(original);
    }

    [Fact]
    public void Encode_EmptyArray_ReturnsEmptyString()
    {
        string encoded = Base64Url.Encode([]);

        encoded.ShouldBeEmpty();
    }

    [Fact]
    public void Encode_ReplacesStandardBase64Characters()
    {
        // 0x3E = '>' which in base64 encodes with '+', 0x3F encodes with '/'
        byte[] data = [0xFB, 0xEF]; // produces "u+8" in standard base64 -> "u-8" in base64url

        string encoded = Base64Url.Encode(data);

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
    }
}

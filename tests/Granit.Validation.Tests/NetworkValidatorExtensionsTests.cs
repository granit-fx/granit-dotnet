// =============================================================================
// Tests - NetworkValidatorExtensions
// =============================================================================
// Url:         RFC 3986, http(s) scheme required
// Ipv4Address: RFC 791, dotted-decimal notation
// Ipv6Address: RFC 4291, full and compressed notation
// MacAddress:  IEEE 802, 6 hex pairs with : or - separator
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class NetworkValidatorExtensionsTests
{
    // =========================================================================
    // Url
    // =========================================================================

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/path?q=1&b=2")]
    [InlineData("https://sub.example.com")]
    [InlineData("https://example.com:8080")]
    [InlineData("https://example.com:8080/path")]
    [InlineData("http://localhost:3000")]
    [InlineData("https://example-site.com")]
    public void Url_ValidValues_PassValidation(string url)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Url();

        ValidationResult result = validator.Validate(new TestModel(url));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.com")]               // No scheme
    [InlineData("ftp://example.com")]         // Non-HTTP scheme
    [InlineData("https://")]                  // No authority
    [InlineData("not a url")]
    [InlineData("://example.com")]            // Missing scheme name
    public void Url_InvalidValues_FailValidation(string? url)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Url();

        ValidationResult result = validator.Validate(new TestModel(url));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Url");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Url");
    }

    // =========================================================================
    // Ipv4Address
    // =========================================================================

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("192.168.1.1")]
    [InlineData("255.255.255.255")]
    [InlineData("10.0.0.1")]
    [InlineData("127.0.0.1")]
    public void Ipv4Address_ValidValues_PassValidation(string ip)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ipv4Address();

        ValidationResult result = validator.Validate(new TestModel(ip));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("256.1.1.1")]                 // Octet > 255
    [InlineData("192.168.1")]                 // Only 3 octets
    [InlineData("192.168.1.1.1")]             // 5 octets
    [InlineData("abc.def.ghi.jkl")]           // Non-numeric
    [InlineData("::1")]                       // IPv6, not IPv4
    public void Ipv4Address_InvalidValues_FailValidation(string? ip)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ipv4Address();

        ValidationResult result = validator.Validate(new TestModel(ip));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Ipv4Address");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Ipv4Address");
    }

    // =========================================================================
    // Ipv6Address
    // =========================================================================

    [Theory]
    [InlineData("::1")]                                       // Loopback
    [InlineData("::")]                                        // Unspecified
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]  // Full form
    [InlineData("2001:db8:85a3::8a2e:370:7334")]              // Compressed
    [InlineData("fe80::1")]                                   // Link-local
    public void Ipv6Address_ValidValues_PassValidation(string ip)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ipv6Address();

        ValidationResult result = validator.Validate(new TestModel(ip));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("192.168.1.1")]               // IPv4, not IPv6
    [InlineData("not:an:ipv6")]
    [InlineData("2001:db8:85a3::8a2e:370g:7334")]  // Invalid hex char
    public void Ipv6Address_InvalidValues_FailValidation(string? ip)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ipv6Address();

        ValidationResult result = validator.Validate(new TestModel(ip));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Ipv6Address");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Ipv6Address");
    }

    // =========================================================================
    // MacAddress
    // =========================================================================

    [Theory]
    [InlineData("00:1A:2B:3C:4D:5E")]        // Colon separator
    [InlineData("00-1A-2B-3C-4D-5E")]        // Dash separator
    [InlineData("aa:bb:cc:dd:ee:ff")]         // Lowercase
    [InlineData("FF:FF:FF:FF:FF:FF")]         // Broadcast
    [InlineData("00:00:00:00:00:00")]         // All zeros
    public void MacAddress_ValidValues_PassValidation(string mac)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).MacAddress();

        ValidationResult result = validator.Validate(new TestModel(mac));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("001A2B3C4D5E")]              // No separator
    [InlineData("00:1A:2B:3C:4D")]            // Only 5 pairs
    [InlineData("00:1A:2B:3C:4D:5E:FF")]      // 7 pairs
    [InlineData("GG:1A:2B:3C:4D:5E")]        // Non-hex character
    [InlineData("00:1A:2B:3C:4D:5")]          // Incomplete last pair
    public void MacAddress_InvalidValues_FailValidation(string? mac)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).MacAddress();

        ValidationResult result = validator.Validate(new TestModel(mac));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:MacAddress");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:MacAddress");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}

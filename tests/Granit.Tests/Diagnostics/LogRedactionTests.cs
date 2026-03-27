// =============================================================================
// Tests - LogRedaction
// =============================================================================
// Verifies PII redaction helpers for structured logging / OpenTelemetry:
//   - Email: preserves prefix + domain, masks the rest
//   - EmailDomain: extracts domain only
//   - Phone: preserves country code prefix + last 2 digits
//   - Token: preserves short prefix + suffix for correlation
//   - IpAddress: masks last octet (IPv4) or truncates after 4th group (IPv6)
//   - Username: preserves short prefix
//   - HashPrefix: deterministic, non-reversible 8-char hex
// =============================================================================

using Granit.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Tests.Diagnostics;

public sealed class LogRedactionTests
{
    // -------------------------------------------------------------------------
    // Email
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("john.doe@example.com", "joh***@example.com")]
    [InlineData("ab@example.com", "ab***@example.com")]
    [InlineData("a@example.com", "a***@example.com")]
    [InlineData("alice.wonderland@corp.net", "ali***@corp.net")]
    public void Email_RedactsLocalPart_PreservesDomain(string input, string expected)
    {
        LogRedaction.Email(input).ShouldBe(expected);
    }

    [Fact]
    public void Email_NoAtSign_ReturnsMask()
    {
        LogRedaction.Email("invalid-email").ShouldBe("***");
    }

    [Fact]
    public void Email_AtSignAtStart_ReturnsMask()
    {
        // "@example.com" has atIndex == 0, which is <= 0
        LogRedaction.Email("@example.com").ShouldBe("***");
    }

    [Fact]
    public void Email_ShortLocalPart_PreservesEntireLocalPart()
    {
        // "ab" has length 2, min(3, 2) = 2
        LogRedaction.Email("ab@test.org").ShouldBe("ab***@test.org");
    }

    // -------------------------------------------------------------------------
    // EmailDomain
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("john.doe@example.com", "example.com")]
    [InlineData("user@sub.domain.co.uk", "sub.domain.co.uk")]
    public void EmailDomain_ExtractsDomainPart(string input, string expected)
    {
        LogRedaction.EmailDomain(input).ShouldBe(expected);
    }

    [Fact]
    public void EmailDomain_NoAtSign_ReturnsUnknown()
    {
        LogRedaction.EmailDomain("no-at-sign").ShouldBe("unknown");
    }

    // -------------------------------------------------------------------------
    // Phone
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("+33612345678", "+336*****78")]
    [InlineData("+1234567890", "+123*****90")]
    public void Phone_PreservesPrefixAndLastTwoDigits(string input, string expected)
    {
        LogRedaction.Phone(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123")]
    [InlineData("12")]
    [InlineData("")]
    public void Phone_ShortInput_ReturnsMask(string input)
    {
        LogRedaction.Phone(input).ShouldBe("***");
    }

    [Fact]
    public void Phone_FiveChars_PreservesPrefixAndSuffix()
    {
        // Length 5: prefixKeep = min(4, 5-2) = 3
        string result = LogRedaction.Phone("12345");
        result.ShouldBe("123*****45");
    }

    // -------------------------------------------------------------------------
    // Token
    // -------------------------------------------------------------------------

    [Fact]
    public void Token_LongToken_PreservesPrefixAndSuffix()
    {
        LogRedaction.Token("dLkj3FDmAbCdEfGh").ShouldBe("dLkj...fGh");
    }

    [Fact]
    public void Token_ExactlyNineChars_PreservesPrefixAndSuffix()
    {
        // Length 9: prefix 4, suffix 3 => "ABCD...GHI"
        LogRedaction.Token("ABCDEFGHI").ShouldBe("ABCD...GHI");
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("1234567")]
    [InlineData("short")]
    [InlineData("")]
    public void Token_ShortToken_ReturnsMask(string input)
    {
        LogRedaction.Token(input).ShouldBe("***");
    }

    // -------------------------------------------------------------------------
    // IpAddress — IPv4
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("192.168.1.42", "192.168.1.***")]
    [InlineData("10.0.0.1", "10.0.0.***")]
    [InlineData("255.255.255.255", "255.255.255.***")]
    public void IpAddress_IPv4_MasksLastOctet(string input, string expected)
    {
        LogRedaction.IpAddress(input).ShouldBe(expected);
    }

    // -------------------------------------------------------------------------
    // IpAddress — IPv6
    // -------------------------------------------------------------------------

    [Fact]
    public void IpAddress_IPv6_TruncatesAfterFourthGroup()
    {
        string result = LogRedaction.IpAddress("2001:0db8:85a3:0000:0000:8a2e:0370:7334");

        result.ShouldBe("2001:0db8:85a3:0000:***");
    }

    [Fact]
    public void IpAddress_IPv6_Short_TruncatesAfterFourthColon()
    {
        string result = LogRedaction.IpAddress("fe80::1:2:3:4");

        // fe80::1:2 has 4 colons at position of the 4th colon
        result.ShouldStartWith("fe80:");
        result.ShouldEndWith(":***");
    }

    [Fact]
    public void IpAddress_NoDotsOrColons_ReturnsMask()
    {
        LogRedaction.IpAddress("localhost").ShouldBe("***");
    }

    [Fact]
    public void IpAddress_IPv6_FewerThanFourColons_ReturnsMask()
    {
        // "a:b:c:d" has only 3 colons — never reaches colonCount == 4 — falls through to mask.
        LogRedaction.IpAddress("a:b:c:d").ShouldBe("***");
    }

    // -------------------------------------------------------------------------
    // Username
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("john_admin", "joh***")]
    [InlineData("alice", "ali***")]
    [InlineData("abcdefghij", "abc***")]
    public void Username_PreservesThreeCharPrefix(string input, string expected)
    {
        LogRedaction.Username(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("ab")]
    [InlineData("a")]
    [InlineData("")]
    public void Username_ThreeOrFewerChars_ReturnsMask(string input)
    {
        LogRedaction.Username(input).ShouldBe("***");
    }

    [Fact]
    public void Username_ExactlyFourChars_PreservesPrefix()
    {
        LogRedaction.Username("abcd").ShouldBe("abc***");
    }

    // -------------------------------------------------------------------------
    // HashPrefix
    // -------------------------------------------------------------------------

    [Fact]
    public void HashPrefix_ReturnsDeterministicEightCharHex()
    {
        string hash = LogRedaction.HashPrefix("john.doe@example.com");

        hash.Length.ShouldBe(8);
        hash.ShouldMatch("^[0-9a-f]{8}$");
    }

    [Fact]
    public void HashPrefix_SameInput_ReturnsSameHash()
    {
        string hash1 = LogRedaction.HashPrefix("test-value");
        string hash2 = LogRedaction.HashPrefix("test-value");

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void HashPrefix_DifferentInputs_ReturnDifferentHashes()
    {
        string hash1 = LogRedaction.HashPrefix("user-a@example.com");
        string hash2 = LogRedaction.HashPrefix("user-b@example.com");

        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void HashPrefix_EmptyString_ReturnsValidHex()
    {
        string hash = LogRedaction.HashPrefix(string.Empty);

        hash.Length.ShouldBe(8);
        hash.ShouldMatch("^[0-9a-f]{8}$");
    }

    // -------------------------------------------------------------------------
    // Cross-method — multiple sensitive values
    // -------------------------------------------------------------------------

    [Fact]
    public void AllMethods_ProduceConsistentMaskPattern()
    {
        // Verify that none of the methods leak the original sensitive data.
        const string email = "sensitive.user@private-domain.com";
        const string phone = "+33699887766";
        const string token = "eyJhbGciOiJIUzI1NiJ9.payload.signature";
        const string ip = "172.16.254.1";
        const string username = "sensitive_admin";

        LogRedaction.Email(email).ShouldNotContain("sensitive.user");
        LogRedaction.Phone(phone).ShouldNotContain("99887766");
        LogRedaction.Token(token).ShouldNotContain("payload");
        LogRedaction.IpAddress(ip).ShouldEndWith(".***");
        LogRedaction.Username(username).ShouldNotContain("sensitive_admin");
    }
}

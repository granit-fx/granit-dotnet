using Granit.Identity.Local.Services;
using Granit.OpenIddict.Services;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Services;

public sealed class DefaultTotpServiceTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DefaultTotpService _sut;

    public DefaultTotpServiceTests()
    {
        // Fixed time: 2026-01-15 12:00:00 UTC
        _clock.Now.Returns(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        _sut = new DefaultTotpService(_clock);
    }

    // =========================================================================
    // GenerateSharedKey
    // =========================================================================

    [Fact]
    public void GenerateSharedKey_ReturnsBase32EncodedString()
    {
        string key = _sut.GenerateSharedKey();

        key.ShouldNotBeNullOrWhiteSpace();
        // 20 bytes = 160 bits, Base32 encodes 5 bits per char → 32 chars
        key.Length.ShouldBe(32);
    }

    [Fact]
    public void GenerateSharedKey_ContainsOnlyBase32Characters()
    {
        string key = _sut.GenerateSharedKey();

        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        foreach (char c in key)
        {
            base32Chars.ShouldContain(c);
        }
    }

    [Fact]
    public void GenerateSharedKey_ProducesDifferentKeysOnEachCall()
    {
        string key1 = _sut.GenerateSharedKey();
        string key2 = _sut.GenerateSharedKey();

        key1.ShouldNotBe(key2);
    }

    // =========================================================================
    // GetQrCodeUri
    // =========================================================================

    [Fact]
    public void GetQrCodeUri_ReturnsValidOtpauthUri()
    {
        string sharedKey = _sut.GenerateSharedKey();

        string uri = _sut.GetQrCodeUri("user@example.com", sharedKey);

        uri.ShouldStartWith("otpauth://totp/");
        uri.ShouldContain("secret=" + sharedKey);
        uri.ShouldContain("issuer=Granit");
        uri.ShouldContain("digits=6");
        uri.ShouldContain("period=30");
    }

    [Fact]
    public void GetQrCodeUri_EncodesEmailInUri()
    {
        string sharedKey = _sut.GenerateSharedKey();

        string uri = _sut.GetQrCodeUri("user@example.com", sharedKey);

        uri.ShouldContain("user%40example.com");
    }

    [Fact]
    public void GetQrCodeUri_IncludesIssuerLabel()
    {
        string sharedKey = _sut.GenerateSharedKey();

        string uri = _sut.GetQrCodeUri("test@test.com", sharedKey);

        uri.ShouldContain("Granit:test%40test.com");
    }

    [Fact]
    public void GetQrCodeUri_NullEmail_ThrowsArgumentException()
    {
        Action act = () => _sut.GetQrCodeUri(null!, "SOMEKEY");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void GetQrCodeUri_EmptyEmail_ThrowsArgumentException()
    {
        Action act = () => _sut.GetQrCodeUri("", "SOMEKEY");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void GetQrCodeUri_WhitespaceEmail_ThrowsArgumentException()
    {
        Action act = () => _sut.GetQrCodeUri("   ", "SOMEKEY");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void GetQrCodeUri_NullSharedKey_ThrowsArgumentException()
    {
        Action act = () => _sut.GetQrCodeUri("user@example.com", null!);

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void GetQrCodeUri_EmptySharedKey_ThrowsArgumentException()
    {
        Action act = () => _sut.GetQrCodeUri("user@example.com", "");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void GetQrCodeUri_WhitespaceSharedKey_ThrowsArgumentException()
    {
        Action act = () => _sut.GetQrCodeUri("user@example.com", "   ");

        Should.Throw<ArgumentException>(act);
    }

    // =========================================================================
    // ValidateCode — input validation
    // =========================================================================

    [Fact]
    public void ValidateCode_NullSharedKey_ThrowsArgumentException()
    {
        Action act = () => _sut.ValidateCode(null!, "123456");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void ValidateCode_EmptySharedKey_ThrowsArgumentException()
    {
        Action act = () => _sut.ValidateCode("", "123456");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void ValidateCode_WhitespaceSharedKey_ThrowsArgumentException()
    {
        Action act = () => _sut.ValidateCode("   ", "123456");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void ValidateCode_NullCode_ReturnsFalse()
    {
        string key = _sut.GenerateSharedKey();

        bool result = _sut.ValidateCode(key, null!);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateCode_EmptyCode_ReturnsFalse()
    {
        string key = _sut.GenerateSharedKey();

        bool result = _sut.ValidateCode(key, "");

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("12345")]    // 5 digits — too short
    [InlineData("1234567")]  // 7 digits — too long
    [InlineData("abcdef")]   // 6 chars but not a valid code match
    public void ValidateCode_WrongLengthOrFormat_ReturnsFalse(string code)
    {
        string key = _sut.GenerateSharedKey();

        bool result = _sut.ValidateCode(key, code);

        result.ShouldBeFalse();
    }

    // =========================================================================
    // ValidateCode — roundtrip (generate key, compute valid code, validate)
    // =========================================================================

    [Fact]
    public void ValidateCode_CorrectCode_ReturnsTrue()
    {
        // Generate a key and compute the expected TOTP for the current time step.
        string key = _sut.GenerateSharedKey();
        string code = ComputeTotpForKey(key, _clock.Now);

        bool result = _sut.ValidateCode(key, code);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_CodeFromPreviousTimeStep_ReturnsTrue()
    {
        // The service allows +/-1 time step for clock drift.
        string key = _sut.GenerateSharedKey();
        DateTimeOffset previousStep = _clock.Now.AddSeconds(-30);
        string code = ComputeTotpForKey(key, previousStep);

        bool result = _sut.ValidateCode(key, code);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_CodeFromNextTimeStep_ReturnsTrue()
    {
        string key = _sut.GenerateSharedKey();
        DateTimeOffset nextStep = _clock.Now.AddSeconds(30);
        string code = ComputeTotpForKey(key, nextStep);

        bool result = _sut.ValidateCode(key, code);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_CodeFromTwoStepsAgo_ReturnsFalse()
    {
        string key = _sut.GenerateSharedKey();
        DateTimeOffset twoStepsAgo = _clock.Now.AddSeconds(-60);
        string code = ComputeTotpForKey(key, twoStepsAgo);

        bool result = _sut.ValidateCode(key, code);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateCode_WrongKey_ReturnsFalse()
    {
        string correctKey = _sut.GenerateSharedKey();
        string wrongKey = _sut.GenerateSharedKey();
        string code = ComputeTotpForKey(correctKey, _clock.Now);

        bool result = _sut.ValidateCode(wrongKey, code);

        result.ShouldBeFalse();
    }

    // =========================================================================
    // Base32 encoding/decoding roundtrip (via GenerateSharedKey + ValidateCode)
    // =========================================================================

    [Fact]
    public void Base32RoundTrip_GeneratedKeyCanBeDecoded()
    {
        // This indirectly tests Base32Encode/Decode through the public API.
        string key = _sut.GenerateSharedKey();
        string code = ComputeTotpForKey(key, _clock.Now);

        // If Base32 encode/decode were broken, this would fail.
        bool result = _sut.ValidateCode(key, code);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_LowercaseBase32Key_IsHandled()
    {
        // The Base32Decode uses char.ToUpperInvariant, so lowercase should also work.
        string key = _sut.GenerateSharedKey();
        string lowercaseKey = key.ToLowerInvariant();
        string code = ComputeTotpForKey(key, _clock.Now);

        bool result = _sut.ValidateCode(lowercaseKey, code);

        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_InvalidBase32Character_ThrowsFormatException()
    {
        // '1' is not in the Base32 alphabet (A-Z, 2-7)
        Action act = () => _sut.ValidateCode("INVALID!KEY@", "123456");

        Should.Throw<FormatException>(act);
    }

    // =========================================================================
    // ITotpService contract
    // =========================================================================

    [Fact]
    public void DefaultTotpService_ImplementsITotpService()
    {
        ITotpService service = _sut;

        service.ShouldNotBeNull();
    }

    // =========================================================================
    // Helpers — compute TOTP externally for verification
    // =========================================================================

    /// <summary>
    /// Computes a TOTP code using the same algorithm as the SUT, for test verification.
    /// Uses the public Base32 decode path through reflection to avoid duplicating logic.
    /// </summary>
    // HMAC-SHA1 is mandated by RFC 6238 for TOTP interoperability with authenticator apps.
#pragma warning disable CA5350 // Do not use weak cryptographic algorithms
    private static string ComputeTotpForKey(string base32Key, DateTimeOffset timestamp)
    {
        byte[] key = Base32DecodeForTest(base32Key);
        long timeStep = timestamp.ToUnixTimeSeconds() / 30;

        Span<byte> timeBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(timeBytes, timeStep);

        Span<byte> hash = stackalloc byte[System.Security.Cryptography.HMACSHA1.HashSizeInBytes];
        System.Security.Cryptography.HMACSHA1.HashData(key, timeBytes, hash);

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString().PadLeft(6, '0');
    }
#pragma warning restore CA5350

    private static byte[] Base32DecodeForTest(string base32)
    {
        ReadOnlySpan<char> trimmed = base32.AsSpan().TrimEnd('=');
        byte[] result = new byte[trimmed.Length * 5 / 8];
        int buffer = 0;
        int bitsLeft = 0;
        int index = 0;

        foreach (char c in trimmed)
        {
            char upper = char.ToUpperInvariant(c);
            int value = upper switch
            {
                >= 'A' and <= 'Z' => upper - 'A',
                >= '2' and <= '7' => upper - '2' + 26,
                _ => throw new FormatException($"Invalid Base32 character: '{c}'.")
            };

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result[index++] = (byte)(buffer >> bitsLeft);
            }
        }

        return result;
    }
}

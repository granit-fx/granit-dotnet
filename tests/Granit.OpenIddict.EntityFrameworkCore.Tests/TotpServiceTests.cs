using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

public sealed class TotpServiceTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IdentityOptions _identityOptions = new();
    private readonly TotpService _sut;

    // 2026-01-15 12:00:00 UTC → Unix 1768478400 → time step 58949280
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public TotpServiceTests()
    {
        _clock.Now.Returns(Now);
        IOptions<IdentityOptions> options = Microsoft.Extensions.Options.Options.Create(_identityOptions);
        _sut = new TotpService(_clock, options);
    }

    [Fact]
    public void GenerateSharedKey_ReturnsValidBase32String()
    {
        string key = _sut.GenerateSharedKey();

        key.ShouldNotBeNullOrWhiteSpace();
        key.ShouldAllBe(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(c));
        key.Length.ShouldBe(32); // 20 bytes → 32 Base32 chars
    }

    [Fact]
    public void GenerateSharedKey_ReturnsDifferentKeysEachCall()
    {
        string key1 = _sut.GenerateSharedKey();
        string key2 = _sut.GenerateSharedKey();

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void GetQrCodeUri_FormatsOtpauthUri()
    {
        _identityOptions.Tokens.AuthenticatorIssuer = "TestApp";
        string key = "JBSWY3DPEHPK3PXP";

        string uri = _sut.GetQrCodeUri("user@example.com", key);

        uri.ShouldStartWith("otpauth://totp/TestApp:user%40example.com?");
        uri.ShouldContain("secret=JBSWY3DPEHPK3PXP");
        uri.ShouldContain("issuer=TestApp");
        uri.ShouldContain("digits=6");
        uri.ShouldContain("period=30");
    }

    [Fact]
    public void GetQrCodeUri_DefaultsIssuerToGranit_WhenNotConfigured()
    {
        _identityOptions.Tokens.AuthenticatorIssuer = null!;

        string uri = _sut.GetQrCodeUri("user@example.com", "JBSWY3DPEHPK3PXP");

        uri.ShouldContain("issuer=Granit");
    }

    [Fact]
    public void GetQrCodeUri_EncodesSpecialCharactersInEmail()
    {
        _identityOptions.Tokens.AuthenticatorIssuer = "My App";

        string uri = _sut.GetQrCodeUri("user+tag@example.com", "JBSWY3DPEHPK3PXP");

        uri.ShouldContain("My%20App");
        uri.ShouldContain("user%2Btag%40example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetQrCodeUri_ThrowsOnInvalidEmail(string email) =>
        Should.Throw<ArgumentException>(() => _sut.GetQrCodeUri(email, "JBSWY3DPEHPK3PXP"));

    [Fact]
    public void GetQrCodeUri_ThrowsOnNullEmail() =>
        Should.Throw<ArgumentException>(() => _sut.GetQrCodeUri(null!, "JBSWY3DPEHPK3PXP"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetQrCodeUri_ThrowsOnInvalidSharedKey(string key) =>
        Should.Throw<ArgumentException>(() => _sut.GetQrCodeUri("user@example.com", key));

    [Fact]
    public void GetQrCodeUri_ThrowsOnNullSharedKey() =>
        Should.Throw<ArgumentException>(() => _sut.GetQrCodeUri("user@example.com", null!));

    [Fact]
    public void ValidateCode_ReturnsTrueForCorrectCode()
    {
        // "Hello!" in Base32 = JBSWY3DPEHPK3PXP (well-known test vector)
        // Pre-compute the expected code for the fixed time step
        string key = _sut.GenerateSharedKey();

        // Generate a code at the current time, then validate it
        // We test round-trip: the implementation validates its own codes
        string code = GenerateCodeForKey(key, Now);

        _sut.ValidateCode(key, code).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_AcceptsCodeFromPreviousTimeStep()
    {
        string key = _sut.GenerateSharedKey();
        DateTimeOffset previousStep = Now.AddSeconds(-30);

        string code = GenerateCodeForKey(key, previousStep);

        _sut.ValidateCode(key, code).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_AcceptsCodeFromNextTimeStep()
    {
        string key = _sut.GenerateSharedKey();
        DateTimeOffset nextStep = Now.AddSeconds(30);

        string code = GenerateCodeForKey(key, nextStep);

        _sut.ValidateCode(key, code).ShouldBeTrue();
    }

    [Fact]
    public void ValidateCode_RejectsCodeFromTwoStepsAgo()
    {
        string key = _sut.GenerateSharedKey();
        DateTimeOffset twoStepsAgo = Now.AddSeconds(-60);

        string code = GenerateCodeForKey(key, twoStepsAgo);

        _sut.ValidateCode(key, code).ShouldBeFalse();
    }

    [Fact]
    public void ValidateCode_RejectsInvalidCode()
    {
        string key = _sut.GenerateSharedKey();

        _sut.ValidateCode(key, "000000").ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    public void ValidateCode_RejectsWrongLengthCode(string code)
    {
        string key = _sut.GenerateSharedKey();

        _sut.ValidateCode(key, code).ShouldBeFalse();
    }

    [Fact]
    public void ValidateCode_RejectsNullCode()
    {
        string key = _sut.GenerateSharedKey();

        _sut.ValidateCode(key, null!).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ValidateCode_ThrowsOnInvalidSharedKey(string key) =>
        Should.Throw<ArgumentException>(() => _sut.ValidateCode(key, "123456"));

    [Fact]
    public void ValidateCode_ThrowsOnNullSharedKey() =>
        Should.Throw<ArgumentException>(() => _sut.ValidateCode(null!, "123456"));

    [Fact]
    public void RoundTrip_GenerateKeyThenValidate()
    {
        string key = _sut.GenerateSharedKey();
        string code = GenerateCodeForKey(key, Now);

        _sut.ValidateCode(key, code).ShouldBeTrue();
    }

    /// <summary>
    /// Replicates the TOTP algorithm to generate expected codes for testing.
    /// </summary>
    private static string GenerateCodeForKey(string base32Key, DateTimeOffset timestamp)
    {
        byte[] key = Base32Decode(base32Key);
        long timeStep = timestamp.ToUnixTimeSeconds() / 30;

        Span<byte> timeBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(timeBytes, timeStep);

        Span<byte> hash = stackalloc byte[20]; // HMACSHA1 = 20 bytes
        System.Security.Cryptography.HMACSHA1.HashData(key, timeBytes, hash);

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString().PadLeft(6, '0');
    }

    private static byte[] Base32Decode(string base32)
    {
        ReadOnlySpan<char> trimmed = base32.AsSpan().TrimEnd('=');
        byte[] result = new byte[trimmed.Length * 5 / 8];
        int buffer = 0;
        int bitsLeft = 0;
        int index = 0;

        foreach (char c in trimmed)
        {
            int value = char.ToUpperInvariant(c) switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= '2' and <= '7' => c - '2' + 26,
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

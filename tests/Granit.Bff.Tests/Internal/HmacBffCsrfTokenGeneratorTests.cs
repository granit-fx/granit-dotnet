using Granit.Bff.Internal;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests.Internal;

public sealed class HmacBffCsrfTokenGeneratorTests
{
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly HmacBffCsrfTokenGenerator _generator;

    public HmacBffCsrfTokenGeneratorTests()
    {
        _clock.Now.Returns(DateTimeOffset.UtcNow);
        _generator = new HmacBffCsrfTokenGenerator(_clock);
    }

    [Fact]
    public void Generate_ReturnsNonEmptyToken()
    {
        string token = _generator.Generate("session-123");

        token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Generate_ReturnsTokenInTimestampHmacFormat()
    {
        string token = _generator.Generate("session-123");

        string[] parts = token.Split(':', 2);
        parts.Length.ShouldBe(2);
        long.TryParse(parts[0], out _).ShouldBeTrue();
        parts[1].ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Validate_ReturnsTrue_ForValidToken()
    {
        string token = _generator.Generate("session-123");

        bool result = _generator.Validate("session-123", token);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForTamperedToken()
    {
        string token = _generator.Generate("session-123");
        string[] parts = token.Split(':', 2);
        string tampered = $"{parts[0]}:{"0".PadLeft(parts[1].Length, '0')}";

        bool result = _generator.Validate("session-123", tampered);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForDifferentSession()
    {
        string token = _generator.Generate("session-123");

        bool result = _generator.Validate("session-different", token);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForExpiredToken()
    {
        DateTimeOffset pastTime = DateTimeOffset.UtcNow.AddHours(-25);
        _clock.Now.Returns(pastTime);
        string token = _generator.Generate("session-123");

        _clock.Now.Returns(DateTimeOffset.UtcNow);
        bool result = _generator.Validate("session-123", token);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ReturnsTrue_ForTokenWithinWindow()
    {
        var now = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now);
        string token = _generator.Generate("session-123");

        // Move clock forward 23 hours (still within 24h window)
        _clock.Now.Returns(now.AddHours(23));
        bool result = _generator.Validate("session-123", token);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForNullOrWhiteSpaceToken()
    {
        _generator.Validate("session-123", null!).ShouldBeFalse();
        _generator.Validate("session-123", "").ShouldBeFalse();
        _generator.Validate("session-123", "   ").ShouldBeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForMalformedToken_NoColon()
    {
        bool result = _generator.Validate("session-123", "no-colon-here");

        result.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForMalformedToken_NonNumericTimestamp()
    {
        bool result = _generator.Validate("session-123", "notanumber:abc123");

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Generate_NullOrEmptySessionId_ThrowsArgumentException(string? sessionId) =>
        Should.Throw<ArgumentException>(() => _generator.Generate(sessionId!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_NullOrEmptySessionId_ThrowsArgumentException(string? sessionId) =>
        Should.Throw<ArgumentException>(() => _generator.Validate(sessionId!, "some-token"));

    [Fact]
    public void Generate_ProducesDifferentTokens_ForDifferentSessions()
    {
        string token1 = _generator.Generate("session-1");
        string token2 = _generator.Generate("session-2");

        token1.ShouldNotBe(token2);
    }

    [Fact]
    public void Generate_ProducesConsistentHmac_ForSameSessionAndTime()
    {
        var fixedTime = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(fixedTime);

        string token1 = _generator.Generate("session-123");
        string token2 = _generator.Generate("session-123");

        // Same session + same timestamp = same token (deterministic HMAC)
        token1.ShouldBe(token2);
    }
}

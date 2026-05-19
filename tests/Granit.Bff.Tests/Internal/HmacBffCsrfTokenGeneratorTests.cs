using Granit.Bff.Internal;
using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
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
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Development");
        _generator = new HmacBffCsrfTokenGenerator(
            _clock,
            Microsoft.Extensions.Options.Options.Create(new GranitBffOptions()),
            environment,
            NullLogger<HmacBffCsrfTokenGenerator>.Instance);
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
        // Validation window is 1 hour. Generate the token 90 minutes in the past
        // and re-validate "now" — must be rejected as expired.
        var now = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now.AddMinutes(-90));
        string token = _generator.Generate("session-123");

        _clock.Now.Returns(now);
        bool result = _generator.Validate("session-123", token);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ReturnsTrue_ForTokenWithinWindow()
    {
        var now = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now);
        string token = _generator.Generate("session-123");

        // Move clock forward 50 minutes (still within the 1-hour window)
        _clock.Now.Returns(now.AddMinutes(50));
        bool result = _generator.Validate("session-123", token);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ReturnsFalse_ForFutureTokenBeyondClockSkew()
    {
        // Future-dated tokens beyond the 60s clock-skew tolerance must be rejected
        // outright — significant skew is more likely forgery than time drift.
        var now = new DateTimeOffset(2026, 3, 22, 12, 0, 0, TimeSpan.Zero);
        _clock.Now.Returns(now.AddMinutes(5));
        string token = _generator.Generate("session-123");

        _clock.Now.Returns(now);
        bool result = _generator.Validate("session-123", token);

        result.ShouldBeFalse();
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

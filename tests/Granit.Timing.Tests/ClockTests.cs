// =============================================================================
// Tests - Clock
// =============================================================================
// Verifies that the Clock implementation:
//   - Returns UTC time via TimeProvider
//   - Normalizes local DateTimeOffset values to UTC
//   - Converts to the user's timezone (ConvertToUserTime)
//   - Converts to UTC (ConvertToUtc)
//   - Works without a configured timezone (returns the value unchanged)
// =============================================================================

using Granit.Timing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class ClockTests
{
    private readonly FakeTimeProvider _fakeTimeProvider;
    private readonly ICurrentTimezoneProvider _timezoneProvider;
    private readonly Clock _clock;

    public ClockTests()
    {
        _fakeTimeProvider = new FakeTimeProvider();
        _timezoneProvider = Substitute.For<ICurrentTimezoneProvider>();
        _clock = new Clock(_fakeTimeProvider, _timezoneProvider);
    }

    [Fact]
    public void Now_ReturnsUtcFromTimeProvider()
    {
        // Arrange
        var expected = new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
        _fakeTimeProvider.SetUtcNow(expected);

        // Act
        DateTimeOffset now = _clock.Now;

        // Assert
        now.ShouldBe(expected);
        now.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Now_IsAlwaysUtc()
    {
        // Act
        DateTimeOffset now = _clock.Now;

        // Assert
        now.Offset.ShouldBe(TimeSpan.Zero, "Clock must always return UTC (ISO 27001 compliance)");
    }

    [Fact]
    public void SupportsMultipleTimezone_ReturnsTrue() => _clock.SupportsMultipleTimezone.ShouldBeTrue();

    [Fact]
    public void Normalize_ConvertsLocalOffsetToUtc()
    {
        // Arrange - DateTimeOffset with offset +02:00 (Europe/Brussels in summer)
        var localTime = new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.FromHours(2));

        // Act
        DateTimeOffset normalized = _clock.Normalize(localTime);

        // Assert - Must be converted to UTC (+00:00), same instant
        normalized.Offset.ShouldBe(TimeSpan.Zero);
        normalized.ShouldBe(new DateTimeOffset(2026, 6, 15, 12, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Normalize_KeepsUtcUnchanged()
    {
        // Arrange
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 30, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset normalized = _clock.Normalize(utcTime);

        // Assert
        normalized.ShouldBe(utcTime);
    }

    [Fact]
    public void Normalize_ConvertsNegativeOffsetToUtc()
    {
        // Arrange - DateTimeOffset with offset -05:00 (America/New_York)
        var localTime = new DateTimeOffset(2026, 6, 15, 7, 30, 0, TimeSpan.FromHours(-5));

        // Act
        DateTimeOffset normalized = _clock.Normalize(localTime);

        // Assert
        normalized.Offset.ShouldBe(TimeSpan.Zero);
        normalized.ShouldBe(new DateTimeOffset(2026, 6, 15, 12, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ConvertToUserTime_WithTimezone_ConvertsCorrectly()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns("Europe/Brussels");
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset userTime = _clock.ConvertToUserTime(utcTime);

        // Assert - Brussels is UTC+2 in summer (CEST)
        // Use .DateTime.Hour (respects the stored offset) not .LocalDateTime.Hour (machine-timezone-dependent)
        userTime.Offset.ShouldBe(TimeSpan.FromHours(2));
        userTime.DateTime.Hour.ShouldBe(14);
    }

    [Fact]
    public void ConvertToUserTime_WithoutTimezone_ReturnsUnchanged()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns((string?)null);
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset userTime = _clock.ConvertToUserTime(utcTime);

        // Assert
        userTime.ShouldBe(utcTime);
    }

    [Fact]
    public void ConvertToUserTime_WithEmptyTimezone_ReturnsUnchanged()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns("");
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset userTime = _clock.ConvertToUserTime(utcTime);

        // Assert
        userTime.ShouldBe(utcTime);
    }

    [Fact]
    public void ConvertToUtc_ConvertsToUtc()
    {
        // Arrange - DateTimeOffset with offset +02:00
        var localTime = new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.FromHours(2));

        // Act
        DateTimeOffset utcTime = _clock.ConvertToUtc(localTime);

        // Assert
        utcTime.Offset.ShouldBe(TimeSpan.Zero);
        utcTime.ShouldBe(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ConvertToUtc_KeepsUtcUnchanged()
    {
        // Arrange
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset result = _clock.ConvertToUtc(utcTime);

        // Assert
        result.ShouldBe(utcTime);
    }

    [Fact]
    public void ConvertToUserTime_WithWhitespaceTimezone_ReturnsUnchanged()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns("   ");
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset userTime = _clock.ConvertToUserTime(utcTime);

        // Assert
        userTime.ShouldBe(utcTime);
    }

    [Fact]
    public void ConvertToUserTime_WithInvalidTimezone_ReturnsUnchanged()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns("Invalid/Timezone");
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset result = _clock.ConvertToUserTime(utcTime);

        // Assert — graceful fallback: invalid timezone ID returns the value unchanged
        result.ShouldBe(utcTime);
    }

    [Fact]
    public void ConvertToUserTime_WithNegativeOffsetTimezone_ConvertsCorrectly()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns("America/New_York");
        var utcTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset userTime = _clock.ConvertToUserTime(utcTime);

        // Assert - New York is UTC-5 in winter (EST)
        userTime.Offset.ShouldBe(TimeSpan.FromHours(-5));
        userTime.DateTime.Hour.ShouldBe(7);
    }

    [Fact]
    public void ConvertToUtc_ConvertsNegativeOffsetToUtc()
    {
        // Arrange - DateTimeOffset with offset -05:00
        var localTime = new DateTimeOffset(2026, 6, 15, 7, 0, 0, TimeSpan.FromHours(-5));

        // Act
        DateTimeOffset utcTime = _clock.ConvertToUtc(localTime);

        // Assert
        utcTime.Offset.ShouldBe(TimeSpan.Zero);
        utcTime.ShouldBe(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ConvertToUserTime_WithUtcTimezone_ReturnsUtc()
    {
        // Arrange
        _timezoneProvider.Timezone.Returns("UTC");
        var utcTime = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset userTime = _clock.ConvertToUserTime(utcTime);

        // Assert
        userTime.Offset.ShouldBe(TimeSpan.Zero);
        userTime.ShouldBe(utcTime);
    }
}

using Granit.Presence.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Options;

public sealed class PresenceOptionsValidatorTests
{
    private readonly PresenceOptionsValidator _validator = new();

    [Fact]
    public void Validate_passes_with_defaults()
    {
        ValidateOptionsResult result = _validator.Validate(null, new PresenceOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_offline_threshold_negative()
    {
        PresenceOptions options = new() { OfflineThreshold = TimeSpan.FromSeconds(-1) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_heartbeat_ttl_smaller_than_offline_threshold()
    {
        PresenceOptions options = new()
        {
            OfflineThreshold = TimeSpan.FromSeconds(120),
            HeartbeatCacheTtl = TimeSpan.FromSeconds(60),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_batch_size_too_large()
    {
        PresenceOptions options = new() { MaxBatchSize = 5000 };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_offline_threshold_exceeds_thirty_minutes()
    {
        PresenceOptions options = new()
        {
            OfflineThreshold = TimeSpan.FromMinutes(31),
            HeartbeatCacheTtl = TimeSpan.FromMinutes(31),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(PresenceOptions.OfflineThreshold));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60 * 60 * 3)] // 3 hours, exceeds the 2-hour ceiling
    public void Validate_fails_when_away_threshold_out_of_range(int seconds)
    {
        PresenceOptions options = new() { AwayThreshold = TimeSpan.FromSeconds(seconds) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(PresenceOptions.AwayThreshold));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)] // 31 days, exceeds the 30-day ceiling
    public void Validate_fails_when_max_override_duration_out_of_range(int days)
    {
        PresenceOptions options = new() { MaxOverrideDuration = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(PresenceOptions.MaxOverrideDuration));
    }

    [Fact]
    public void Validate_aggregates_every_failure()
    {
        PresenceOptions options = new()
        {
            OfflineThreshold = TimeSpan.Zero,
            AwayThreshold = TimeSpan.Zero,
            HeartbeatCacheTtl = TimeSpan.FromSeconds(-1),
            MaxBatchSize = 0,
            MaxOverrideDuration = TimeSpan.Zero,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBeGreaterThanOrEqualTo(4);
    }
}
